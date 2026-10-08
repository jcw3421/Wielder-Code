using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Game.BossContent
{
    public struct TfwWarningState : INetworkStruct
    {
        public int Shape, Sequence, StartTick, EndTick;
        public Vector3 Center, Dimensions;
        public Quaternion Rotation;
    }

    // The server publishes impending hits from the same definition and repeat timings as damage.
    // No local stopwatch, RPC-only warning or damage simulation on clients.
    public sealed class TfwTelegraphPlan
    {
        public const float LeadSeconds = .8f;
        private struct Hit { public float At, Reach, Yaw; public int Shape; public Vector3 Size; }
        private readonly List<Hit> _hits = new List<Hit>();
        private readonly TfwPatternDefinition _def;
        private readonly BossServices _services;
        private readonly BossPatternEffectPlayer _view;

        public TfwTelegraphPlan(PatternDefinition def, BossServices services)
        {
            _def=def as TfwPatternDefinition; _services=services; _view=services.Net.GetComponent<BossPatternEffectPlayer>();
            Build(); _hits.Sort((a,b)=>a.At.CompareTo(b.At));
        }
        private void Add(string key,float at,int shape,Vector3 size,float reach=0,float yaw=0)
        {
            int count=key==null?1:_def.EffectiveHitCount(key,at);
            for(int i=0;i<count;i++) _hits.Add(new Hit { At=key==null?at:_def.RepeatTimeAt(key,at,i),Shape=shape,Size=size,Reach=reach,Yaw=yaw });
        }
        private void Sphere(string key,float at,float radius,float reach=0)=>Add(key,at,0,new Vector3(radius,0,0),reach);
        private void Fan(string key,float at,float radius,float angle,float yaw=0)=>Add(key,at,3,new Vector3(radius,0,angle),0,yaw);
        private void Box(string key,float at,float width,float height,float length,float reach,float yaw=0)=>Add(key,at,2,new Vector3(width,height,length)*.5f,reach,yaw);

        public void Tick(int elapsedTicks,float speed)
        {
            if(_def==null || _view==null || !_services.Net.HasStateAuthority)return;
            float rate=_services.Stats.TickRate;
            int now=_services.Net.Runner.Tick.Raw;
            int slot=0;
            float elapsed=elapsedTicks*speed/rate;
            for(int i=0;i<_hits.Count&&slot<31;i++)
            {
                var hit=_hits[i]; int due=Mathf.RoundToInt(hit.At*rate/speed);
                int lead=Mathf.RoundToInt(LeadSeconds*rate);
                if(i==0)lead=Mathf.Max(lead,Mathf.RoundToInt(_def.TelegraphSeconds*rate/speed)+due);
                if(elapsedTicks>=due || elapsedTicks<due-lead)continue;
                // Repeated hits of the same area share one countdown, then advance to the next hit.
                bool duplicate=false;
                for(int j=0;j<i;j++)
                    if(Mathf.RoundToInt(_hits[j].At*rate/speed)>elapsedTicks && _hits[j].Shape==hit.Shape &&
                        (_hits[j].Size-hit.Size).sqrMagnitude<.000001f && Mathf.Abs(_hits[j].Reach-hit.Reach)<.001f && Mathf.Abs(_hits[j].Yaw-hit.Yaw)<.001f) {duplicate=true;break;}
                if(duplicate)continue;
                float turn=TurnedAt(hit.At)!=TurnedAt(elapsed)?180:0;
                Quaternion rotation=Quaternion.LookRotation(_services.Motor.Forward)*Quaternion.Euler(0,hit.Yaw+turn,0);
                // Horizontal projection of the hit volume, drawn just above the boss foot plane.
                Vector3 center=_services.Motor.Position+rotation*Vector3.forward*hit.Reach+Vector3.up*.055f;
                _view.SetWarning(slot++,new TfwWarningState { Shape=hit.Shape,Dimensions=hit.Size,Center=center,Rotation=rotation,
                    Sequence=_services.Net.PatternStartSeq,StartTick=now+due-elapsedTicks-lead,EndTick=now+due-elapsedTicks });
            }
            for(;slot<31;slot++)_view.SetWarning(slot,default);
        }
        private bool TurnedAt(float at)
        {
            if(at<0 || !(_def is ITfwComboDefinition combo) || combo.SegmentStartSeconds==null)return false;
            int segment=0;
            for(int i=0;i<combo.SegmentStartSeconds.Length;i++)if(at>=combo.SegmentStartSeconds[i])segment=i;
            if(combo.TurnAroundSegments!=null)foreach(int i in combo.TurnAroundSegments)if(i==segment)return true;
            return false;
        }
        private void Build()
        {
            switch(_def)
            {
                case CleaveComboDefinition d:
                    Sphere(nameof(d.SpinAtSeconds),d.SpinAtSeconds,d.SpinRadius);
                    Sphere(nameof(d.BackhandAtSeconds),d.BackhandAtSeconds,d.BackhandRadius,d.BackhandReach);
                    Box(nameof(d.CleaveAtSeconds),d.CleaveAtSeconds,d.CleaveWidth,d.CleaveHeight,d.CleaveLength,d.CleaveReach); break;
                case RisingComboDefinition d:
                    Fan(nameof(d.RightRiseAtSeconds),d.RightRiseAtSeconds,d.RiseRadius,d.RiseHalfAngle,d.RiseYawOffset);
                    Fan(nameof(d.LeftRiseAtSeconds),d.LeftRiseAtSeconds,d.RiseRadius,d.RiseHalfAngle,-d.RiseYawOffset);
                    Box(nameof(d.CleaveAtSeconds),d.CleaveAtSeconds,d.CleaveWidth,d.CleaveHeight,d.CleaveLength,d.CleaveReach); break;
                case FourStrikeComboDefinition d:
                    Box(nameof(d.ThrustAtSeconds),d.ThrustAtSeconds,d.ThrustWidth,d.StrikeHeight,d.ThrustLength,d.ThrustReach);
                    Fan(nameof(d.FinishAtSeconds),d.FinishAtSeconds,d.FinishRadius,d.FinishHalfAngle);
                    Box(nameof(d.FrontCleaveAtSeconds),d.FrontCleaveAtSeconds,d.CleaveWidth,d.StrikeHeight,d.CleaveLength,d.CleaveReach);
                    Box(nameof(d.BackCleaveAtSeconds),d.BackCleaveAtSeconds,d.CleaveWidth,d.StrikeHeight,d.CleaveLength,d.CleaveReach); break;
                case SwordShieldComboDefinition d:
                    Fan(nameof(d.Slash1AtSeconds),d.Slash1AtSeconds,d.SlashRadius,d.SlashHalfAngle);
                    Sphere(nameof(d.ShieldAtSeconds),d.ShieldAtSeconds,d.ShieldRadius,d.ShieldReach);
                    Fan(nameof(d.Slash2AtSeconds),d.Slash2AtSeconds,d.SlashRadius,d.SlashHalfAngle);
                    Fan(nameof(d.BackTurnAtSeconds),d.BackTurnAtSeconds,d.BackTurnRadius,d.BackTurnHalfAngle);
                    Box(nameof(d.ThrustAtSeconds),d.ThrustAtSeconds,d.ThrustWidth,d.StrikeHeight,d.ThrustLength,d.ThrustReach); break;
                case ShieldBashComboDefinition d:
                    Fan(nameof(d.SlashAtSeconds),d.SlashAtSeconds,d.SlashRadius,d.SlashHalfAngle);
                    Sphere(nameof(d.ShieldAtSeconds),d.ShieldAtSeconds,d.ShieldRadius,d.ShieldReach);
                    Fan(nameof(d.CrossSlashAtSeconds),d.CrossSlashAtSeconds,d.CrossRadius,d.CrossHalfAngle);
                    Sphere(nameof(d.BashAtSeconds),d.BashAtSeconds,d.BashRadius,d.BashReach);
                    Sphere(nameof(d.KickAtSeconds),d.KickAtSeconds,d.KickRadius,d.KickReach); break;
                case RushSixStrikeDefinition d:
                    for(int i=0;i<d.StrikeCount;i++)Fan(null,d.FirstStrikeAtSeconds+i*d.StrikeIntervalSeconds,d.StrikeRadius,d.StrikeHalfAngle);
                    Box(nameof(d.ThrustAtSeconds),d.ThrustAtSeconds,d.ThrustWidth,d.StrikeHeight,d.ThrustLength,d.ThrustReach); break;
                case CascadeFieldComboDefinition d:
                    Sphere(nameof(d.ShieldAtSeconds),d.ShieldAtSeconds,d.ShieldRadius,d.ShieldReach);
                    Fan(nameof(d.SlashAtSeconds),d.SlashAtSeconds,d.SlashRadius,d.SlashHalfAngle);
                    Fan(nameof(d.CrossSlashAtSeconds),d.CrossSlashAtSeconds,d.CrossRadius,d.CrossHalfAngle); break;
                case LaserComboDefinition d:
                    Sphere(nameof(d.ShieldAtSeconds),d.ShieldAtSeconds,d.ShieldRadius,d.ShieldReach);
                    for(int volley=0;volley<d.LaserVolleys;volley++)for(int i=0;i<Mathf.Max(2,d.LaserCount);i++)
                    {
                        Vector3 dir=d.LaserDirection(Vector3.forward,i,volley==0?0:d.SecondVolleyOffset);
                        Box(null,volley==0?d.FrontLaserAtSeconds:d.BackLaserAtSeconds,d.LaserWidth,d.LaserHeight,d.LaserLength,d.LaserLength*.5f,Mathf.Atan2(dir.x,dir.z)*Mathf.Rad2Deg);
                    } break;
                case DonutFieldDefinition d:
                    Add(null,d.BurstAtSeconds,1,new Vector3(d.OuterRadius,d.InnerRadius,0)); break;
                case TriBlastDefinition d:
                    Sphere(null,d.NearAtSeconds,d.NearRadius);Sphere(null,d.MidAtSeconds,d.MidRadius);Sphere(null,d.FarAtSeconds,d.FarRadius); break;
                case SunderingDashDefinition d:
                    Sphere(null,0,d.PathRadius,d.HitFrontOffset);
                    Sphere(null,d.ActiveSeconds,d.FinishRadius,d.FinishReach);break;
            }
        }
    }
}
