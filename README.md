# Wielder (최초의 윌더) — 정창우 작성 코드

> 3D 쿼터뷰 온라인 4인 협동 보스런 · 팀 4인 · 2026.06 – 09 · STOVE 출시  
> Unity 6 · C# · Photon Fusion 2  
> 내 커밋 기간: 2026-06-25 – 2026-09-10

Unity 프로젝트 전체가 아니라, **정창우(jcw3421)가 실제로 커밋해서 만들거나 수정한 코드 파일만** 모아 둔 레포입니다.
아트 · 사운드 · 씬 · 프리팹 · 에셋스토어 패키지 · 서드파티 코드는 모두 빠져 있어서 이 레포만으로는 빌드되지 않습니다.

## 추출 기준

- 원본: 디벨로켓 부트캠프 조직(`Devel-Rocket-ClassRoom`)의 프로젝트 레포 — 모든 브랜치의 커밋을 조사
- 커밋 작성자가 `jcw3421 <gatecrow@naver.com>` 또는 `정창우 <jcw3421@gmail.com>` 인 커밋에서 추가 · 수정된 코드 파일 (`.cs` `.shader` `.hlsl` 등)
- 직접 만든 파일은 전부 포함, 남이 만든 파일은 현재 버전에 내가 쓴 줄이 5줄 이상 남아 있을 때만 포함
- 파일 내용은 기본 브랜치의 최신 버전 (기본 브랜치에 없으면 해당 파일이 있는 가장 최근 커밋)
- `내 줄 / 전체` 는 `git blame -w` 기준으로 지금 남아 있는 줄 중 내가 쓴 줄 수. 팀 프로젝트라 수정한 파일에는 팀원이 쓴 코드도 함께 들어 있습니다.

## 요약

| 내 커밋 | 포함 파일 | 직접 만든 파일 | 수정한 파일 | 남아 있는 내 코드 줄 |
|---|---|---|---|---|
| 350 | 173 | 144 | 29 | 29,568 |

## 직접 만든 파일

| 파일 | 내 줄 / 전체 | 비율 |
|---|---|---|
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossAuraVelocity.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossAuraVelocity.cs) | 222 / 222 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/BossBladeAura.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/BossBladeAura.cs) | 104 / 104 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/BossGroundDust.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/BossGroundDust.cs) | 190 / 190 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/BossPatternEffectPlayer.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/BossPatternEffectPlayer.cs) | 244 / 244 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/BossTelegraphMarker.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/BossTelegraphMarker.cs) | 40 / 40 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/BossWeaponTrail.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/BossWeaponTrail.cs) | 224 / 224 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/CascadeFieldCombo.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/CascadeFieldCombo.cs) | 73 / 73 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/CascadeFieldComboDefinition.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/CascadeFieldComboDefinition.cs) | 79 / 79 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/CleaveCombo.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/CleaveCombo.cs) | 58 / 58 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/CleaveComboDefinition.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/CleaveComboDefinition.cs) | 88 / 88 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/DonutField.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/DonutField.cs) | 27 / 27 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/DonutFieldDefinition.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/DonutFieldDefinition.cs) | 61 / 61 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/FourStrikeCombo.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/FourStrikeCombo.cs) | 56 / 56 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/FourStrikeComboDefinition.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/FourStrikeComboDefinition.cs) | 96 / 96 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/LaserCombo.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/LaserCombo.cs) | 98 / 98 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/LaserComboDefinition.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/LaserComboDefinition.cs) | 121 / 121 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/MarkedStrike.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/MarkedStrike.cs) | 57 / 57 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/MarkedStrikeDefinition.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/MarkedStrikeDefinition.cs) | 65 / 65 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/PatternEffectCue.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/PatternEffectCue.cs) | 61 / 61 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/PatternFieldDecal.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/PatternFieldDecal.cs) | 71 / 71 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/RisingCombo.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/RisingCombo.cs) | 53 / 53 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/RisingComboDefinition.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/RisingComboDefinition.cs) | 79 / 79 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/RushSixStrike.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/RushSixStrike.cs) | 53 / 53 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/RushSixStrikeDefinition.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/RushSixStrikeDefinition.cs) | 94 / 94 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/ShieldBashCombo.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/ShieldBashCombo.cs) | 63 / 63 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/ShieldBashComboDefinition.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/ShieldBashComboDefinition.cs) | 85 / 85 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/SunderingDash.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/SunderingDash.cs) | 150 / 150 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/SunderingDashDefinition.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/SunderingDashDefinition.cs) | 52 / 52 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/SwordShieldCombo.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/SwordShieldCombo.cs) | 65 / 65 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/SwordShieldComboDefinition.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/SwordShieldComboDefinition.cs) | 89 / 89 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/TfwBossEntrance.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/TfwBossEntrance.cs) | 693 / 693 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/TfwComboPattern.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/TfwComboPattern.cs) | 387 / 387 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/TfwEntranceWeaponRite.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/TfwEntranceWeaponRite.cs) | 386 / 386 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/TfwHits.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/TfwHits.cs) | 122 / 122 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/TfwIaidoExplosionVisual.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/TfwIaidoExplosionVisual.cs) | 24 / 24 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/TfwIaidoFieldLink.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/TfwIaidoFieldLink.cs) | 95 / 95 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/TfwImpactVfx.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/TfwImpactVfx.cs) | 110 / 110 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/TfwPatternDefinition.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/TfwPatternDefinition.cs) | 149 / 149 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/TfwPatternPhase.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/TfwPatternPhase.cs) | 29 / 29 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/TfwSceneUnloadGuard.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/TfwSceneUnloadGuard.cs) | 106 / 106 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/TfwTelegraphPlan.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/TfwTelegraphPlan.cs) | 128 / 128 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/TriBlast.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/TriBlast.cs) | 60 / 60 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/TFW/TriBlastDefinition.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/TFW/TriBlastDefinition.cs) | 57 / 57 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPhaseWeaponSwitcher.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPhaseWeaponSwitcher.cs) | 68 / 68 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Vfx/BackhandElectricSweepVfx.cs`](Assets/_Project/BossContent/02.%20Scripts/Vfx/BackhandElectricSweepVfx.cs) | 336 / 336 | 100% |
| [`Assets/_Project/BossContent/02. Scripts/Vfx/CleaveLandingLightningVfx.cs`](Assets/_Project/BossContent/02.%20Scripts/Vfx/CleaveLandingLightningVfx.cs) | 443 / 443 | 100% |
| [`Assets/_Project/BossContent/06. Effects/TFW/Shaders/TFW_LightningDecal.shader`](Assets/_Project/BossContent/06.%20Effects/TFW/Shaders/TFW_LightningDecal.shader) | 78 / 78 | 100% |
| [`Assets/_Project/BossContent/06. Effects/TFW/Shaders/TFW_LightningRibbon.shader`](Assets/_Project/BossContent/06.%20Effects/TFW/Shaders/TFW_LightningRibbon.shader) | 78 / 78 | 100% |
| [`Assets/_Project/BossContent/06. Effects/TFW/Shaders/TFW_OuterAuraParticle.shader`](Assets/_Project/BossContent/06.%20Effects/TFW/Shaders/TFW_OuterAuraParticle.shader) | 134 / 134 | 100% |
| [`Assets/_Project/BossContent/06. Effects/TFW/Shaders/TFW_RemadeImpact.shader`](Assets/_Project/BossContent/06.%20Effects/TFW/Shaders/TFW_RemadeImpact.shader) | 88 / 88 | 100% |
| [`Assets/_Project/BossContent/10. Editor/TheFirstWielderAnimatorSetup.cs`](Assets/_Project/BossContent/10.%20Editor/TheFirstWielderAnimatorSetup.cs) | 430 / 430 | 100% |
| [`Assets/_Project/BossContent/10. Editor/TheFirstWielderBodyAuraSetup.cs`](Assets/_Project/BossContent/10.%20Editor/TheFirstWielderBodyAuraSetup.cs) | 351 / 351 | 100% |
| [`Assets/_Project/BossContent/10. Editor/TheFirstWielderComboBaker.cs`](Assets/_Project/BossContent/10.%20Editor/TheFirstWielderComboBaker.cs) | 482 / 482 | 100% |
| [`Assets/_Project/BossContent/10. Editor/TheFirstWielderEffectSetup.cs`](Assets/_Project/BossContent/10.%20Editor/TheFirstWielderEffectSetup.cs) | 3138 / 3138 | 100% |
| [`Assets/_Project/BossContent/10. Editor/TheFirstWielderEntranceSetup.cs`](Assets/_Project/BossContent/10.%20Editor/TheFirstWielderEntranceSetup.cs) | 767 / 767 | 100% |
| [`Assets/_Project/BossContent/10. Editor/TheFirstWielderSetup.cs`](Assets/_Project/BossContent/10.%20Editor/TheFirstWielderSetup.cs) | 380 / 380 | 100% |
| [`Assets/_Project/BossContent/10. Editor/TheFirstWielderTunerWindow.cs`](Assets/_Project/BossContent/10.%20Editor/TheFirstWielderTunerWindow.cs) | 1761 / 1761 | 100% |
| [`Assets/_Project/Combat/Editor/AnimSpeedParameterInstaller.cs`](Assets/_Project/Combat/Editor/AnimSpeedParameterInstaller.cs) | 122 / 122 | 100% |
| [`Assets/_Project/Combat/Editor/AnimationSoundEvents.cs`](Assets/_Project/Combat/Editor/AnimationSoundEvents.cs) | 193 / 193 | 100% |
| [`Assets/_Project/Combat/Editor/ClipSpeedBaker.cs`](Assets/_Project/Combat/Editor/ClipSpeedBaker.cs) | 188 / 188 | 100% |
| [`Assets/_Project/Combat/Editor/ClipTrimmer.cs`](Assets/_Project/Combat/Editor/ClipTrimmer.cs) | 243 / 243 | 100% |
| [`Assets/_Project/Combat/Editor/EffectOffsetWindow.cs`](Assets/_Project/Combat/Editor/EffectOffsetWindow.cs) | 2790 / 2790 | 100% |
| [`Assets/_Project/Combat/Editor/ParticleEditWindow.cs`](Assets/_Project/Combat/Editor/ParticleEditWindow.cs) | 473 / 473 | 100% |
| [`Assets/_Project/Combat/Editor/ParticleOffsetMigration.cs`](Assets/_Project/Combat/Editor/ParticleOffsetMigration.cs) | 87 / 87 | 100% |
| [`Assets/_Project/Combat/Editor/PoseOffsetBaker.cs`](Assets/_Project/Combat/Editor/PoseOffsetBaker.cs) | 294 / 294 | 100% |
| [`Assets/_Project/Combat/Runtime/Player/CameraShaker.cs`](Assets/_Project/Combat/Runtime/Player/CameraShaker.cs) | 176 / 176 | 100% |
| [`Assets/_Project/Combat/Runtime/Player/PlayerAnimationSound.cs`](Assets/_Project/Combat/Runtime/Player/PlayerAnimationSound.cs) | 48 / 48 | 100% |
| [`Assets/_Project/Combat/Runtime/Player/PlayerCameraController.cs`](Assets/_Project/Combat/Runtime/Player/PlayerCameraController.cs) | 164 / 216 | 76% |
| [`Assets/_Project/Combat/Runtime/Player/PlayerCombatStats.cs`](Assets/_Project/Combat/Runtime/Player/PlayerCombatStats.cs) | 71 / 71 | 100% |
| [`Assets/_Project/Combat/Runtime/Player/States/DashState.cs`](Assets/_Project/Combat/Runtime/Player/States/DashState.cs) | 49 / 49 | 100% |
| [`Assets/_Project/Combat/Runtime/Player/WeaponModelSelector.cs`](Assets/_Project/Combat/Runtime/Player/WeaponModelSelector.cs) | 44 / 44 | 100% |
| [`Assets/_Project/Combat/Runtime/Profile/CharacterProfilePanel.cs`](Assets/_Project/Combat/Runtime/Profile/CharacterProfilePanel.cs) | 448 / 449 | 100% |
| [`Assets/_Project/Combat/Runtime/Ultimate/ChainController.cs`](Assets/_Project/Combat/Runtime/Ultimate/ChainController.cs) | 168 / 187 | 90% |
| [`Assets/_Project/Combat/Runtime/Weapon/TestSwordWeapon.cs`](Assets/_Project/Combat/Runtime/Weapon/TestSwordWeapon.cs) | 45 / 166 | 27% |
| [`Assets/_Project/Combat/Runtime/Weapon/WeaponBase.cs`](Assets/_Project/Combat/Runtime/Weapon/WeaponBase.cs) | 544 / 802 | 68% |
| [`Assets/_Project/Combat/Runtime/Weapon/WeaponClass.cs`](Assets/_Project/Combat/Runtime/Weapon/WeaponClass.cs) | 13 / 13 | 100% |
| [`Assets/_Project/Combat/Runtime/Weapon/WeaponStamina.cs`](Assets/_Project/Combat/Runtime/Weapon/WeaponStamina.cs) | 134 / 134 | 100% |
| [`Assets/_Project/Combat/Runtime/Weapon/Weapon_Berserker.cs`](Assets/_Project/Combat/Runtime/Weapon/Weapon_Berserker.cs) | 245 / 245 | 100% |
| [`Assets/_Project/Core/Runtime/CameraShakeStrength.cs`](Assets/_Project/Core/Runtime/CameraShakeStrength.cs) | 16 / 16 | 100% |
| [`Assets/_Project/Core/Runtime/DataBases/Records/AttackParticle.cs`](Assets/_Project/Core/Runtime/DataBases/Records/AttackParticle.cs) | 39 / 39 | 100% |
| [`Assets/_Project/Core/Runtime/IChatService.cs`](Assets/_Project/Core/Runtime/IChatService.cs) | 13 / 13 | 100% |
| [`Assets/_Project/Core/Runtime/Onboarding/IOnboardingRules.cs`](Assets/_Project/Core/Runtime/Onboarding/IOnboardingRules.cs) | 27 / 27 | 100% |
| [`Assets/_Project/Core/Runtime/Onboarding/OnboardingEvents.cs`](Assets/_Project/Core/Runtime/Onboarding/OnboardingEvents.cs) | 23 / 23 | 100% |
| [`Assets/_Project/Core/Runtime/Onboarding/OnboardingTypes.cs`](Assets/_Project/Core/Runtime/Onboarding/OnboardingTypes.cs) | 66 / 66 | 100% |
| [`Assets/_Project/JCW_Ui/shader/HorizontalEdgeFade.shader`](Assets/_Project/JCW_Ui/shader/HorizontalEdgeFade.shader) | 104 / 104 | 100% |
| [`Assets/_Project/JCW_Ui/shader/OrnateGoldFrame.shader`](Assets/_Project/JCW_Ui/shader/OrnateGoldFrame.shader) | 284 / 284 | 100% |
| [`Assets/_Project/JCW_Ui/shader/PunkGraphicFrame.shader`](Assets/_Project/JCW_Ui/shader/PunkGraphicFrame.shader) | 295 / 295 | 100% |
| [`Assets/_Project/JCW_Ui/shader/StylizedUiFrame.shader`](Assets/_Project/JCW_Ui/shader/StylizedUiFrame.shader) | 284 / 284 | 100% |
| [`Assets/_Project/Networking/Runtime/PlayerChat.cs`](Assets/_Project/Networking/Runtime/PlayerChat.cs) | 79 / 79 | 100% |
| [`Assets/_Project/Onboarding/Runtime/OnboardingChecklist.cs`](Assets/_Project/Onboarding/Runtime/OnboardingChecklist.cs) | 36 / 36 | 100% |
| [`Assets/_Project/Onboarding/Runtime/OnboardingCourseProvider.cs`](Assets/_Project/Onboarding/Runtime/OnboardingCourseProvider.cs) | 28 / 28 | 100% |
| [`Assets/_Project/Onboarding/Runtime/OnboardingExitPortal.cs`](Assets/_Project/Onboarding/Runtime/OnboardingExitPortal.cs) | 43 / 43 | 100% |
| [`Assets/_Project/Onboarding/Runtime/OnboardingInventoryIsolator.cs`](Assets/_Project/Onboarding/Runtime/OnboardingInventoryIsolator.cs) | 150 / 150 | 100% |
| [`Assets/_Project/Onboarding/Runtime/OnboardingRunner.cs`](Assets/_Project/Onboarding/Runtime/OnboardingRunner.cs) | 179 / 179 | 100% |
| [`Assets/_Project/Onboarding/Runtime/OnboardingSession.cs`](Assets/_Project/Onboarding/Runtime/OnboardingSession.cs) | 51 / 51 | 100% |
| [`Assets/_Project/Onboarding/Runtime/WeaponOnboardingProvider.cs`](Assets/_Project/Onboarding/Runtime/WeaponOnboardingProvider.cs) | 63 / 63 | 100% |
| [`Assets/_Project/Title/PressAnyKeyPrompt.cs`](Assets/_Project/Title/PressAnyKeyPrompt.cs) | 58 / 97 | 60% |
| [`Assets/_Project/Title/SceneLoader.cs`](Assets/_Project/Title/SceneLoader.cs) | 46 / 46 | 100% |
| [`Assets/_Project/Title/Title/SwordDrawSequence.cs`](Assets/_Project/Title/Title/SwordDrawSequence.cs) | 94 / 94 | 100% |
| [`Assets/_Project/Title/Title/TitleStartSequence.cs`](Assets/_Project/Title/Title/TitleStartSequence.cs) | 218 / 218 | 100% |
| [`Assets/_Project/UI/Runtime/DamageMeterPresenter.cs`](Assets/_Project/UI/Runtime/DamageMeterPresenter.cs) | 235 / 235 | 100% |
| [`Assets/_Project/UI/Runtime/DamageMeterView.cs`](Assets/_Project/UI/Runtime/DamageMeterView.cs) | 616 / 616 | 100% |
| [`Assets/_Project/UI/Runtime/JCW_Script/Config/BuffIconCatalog.cs`](Assets/_Project/UI/Runtime/JCW_Script/Config/BuffIconCatalog.cs) | 32 / 32 | 100% |
| [`Assets/_Project/UI/Runtime/JCW_Script/Config/DebuffIconCatalog.cs`](Assets/_Project/UI/Runtime/JCW_Script/Config/DebuffIconCatalog.cs) | 32 / 32 | 100% |
| [`Assets/_Project/UI/Runtime/JCW_Script/DebuffTester.cs`](Assets/_Project/UI/Runtime/JCW_Script/DebuffTester.cs) | 74 / 74 | 100% |
| [`Assets/_Project/UI/Runtime/JCW_Script/Hud/HudOrchestrator.cs`](Assets/_Project/UI/Runtime/JCW_Script/Hud/HudOrchestrator.cs) | 312 / 325 | 96% |
| [`Assets/_Project/UI/Runtime/JCW_Script/Panels/BossUiPanel.cs`](Assets/_Project/UI/Runtime/JCW_Script/Panels/BossUiPanel.cs) | 112 / 127 | 88% |
| [`Assets/_Project/UI/Runtime/JCW_Script/Panels/ChainVignette.cs`](Assets/_Project/UI/Runtime/JCW_Script/Panels/ChainVignette.cs) | 139 / 139 | 100% |
| [`Assets/_Project/UI/Runtime/JCW_Script/Panels/ChatBubble.cs`](Assets/_Project/UI/Runtime/JCW_Script/Panels/ChatBubble.cs) | 245 / 245 | 100% |
| [`Assets/_Project/UI/Runtime/JCW_Script/Panels/ChatBubblePresenter.cs`](Assets/_Project/UI/Runtime/JCW_Script/Panels/ChatBubblePresenter.cs) | 99 / 99 | 100% |
| [`Assets/_Project/UI/Runtime/JCW_Script/Panels/ChatPanel.cs`](Assets/_Project/UI/Runtime/JCW_Script/Panels/ChatPanel.cs) | 272 / 272 | 100% |
| [`Assets/_Project/UI/Runtime/JCW_Script/Panels/MapPanel.cs`](Assets/_Project/UI/Runtime/JCW_Script/Panels/MapPanel.cs) | 29 / 29 | 100% |
| [`Assets/_Project/UI/Runtime/JCW_Script/Panels/PartyUiPanel.cs`](Assets/_Project/UI/Runtime/JCW_Script/Panels/PartyUiPanel.cs) | 84 / 90 | 93% |
| [`Assets/_Project/UI/Runtime/JCW_Script/Panels/PlayerUiPanel.cs`](Assets/_Project/UI/Runtime/JCW_Script/Panels/PlayerUiPanel.cs) | 113 / 113 | 100% |
| [`Assets/_Project/UI/Runtime/JCW_Script/Panels/QuickBarPanel.cs`](Assets/_Project/UI/Runtime/JCW_Script/Panels/QuickBarPanel.cs) | 42 / 42 | 100% |
| [`Assets/_Project/UI/Runtime/JCW_Script/Views/BossStatusBar.cs`](Assets/_Project/UI/Runtime/JCW_Script/Views/BossStatusBar.cs) | 94 / 94 | 100% |
| [`Assets/_Project/UI/Runtime/JCW_Script/Views/ChainCutInSlot.cs`](Assets/_Project/UI/Runtime/JCW_Script/Views/ChainCutInSlot.cs) | 143 / 143 | 100% |
| [`Assets/_Project/UI/Runtime/JCW_Script/Views/ChainLinkView.cs`](Assets/_Project/UI/Runtime/JCW_Script/Views/ChainLinkView.cs) | 22 / 22 | 100% |
| [`Assets/_Project/UI/Runtime/JCW_Script/Views/ChainStageBanner.cs`](Assets/_Project/UI/Runtime/JCW_Script/Views/ChainStageBanner.cs) | 273 / 273 | 100% |
| [`Assets/_Project/UI/Runtime/JCW_Script/Views/ClassIconSelector.cs`](Assets/_Project/UI/Runtime/JCW_Script/Views/ClassIconSelector.cs) | 32 / 32 | 100% |
| [`Assets/_Project/UI/Runtime/JCW_Script/Views/GuardResultBanner.cs`](Assets/_Project/UI/Runtime/JCW_Script/Views/GuardResultBanner.cs) | 176 / 176 | 100% |
| [`Assets/_Project/UI/Runtime/JCW_Script/Views/PartyCutInStack.cs`](Assets/_Project/UI/Runtime/JCW_Script/Views/PartyCutInStack.cs) | 136 / 136 | 100% |
| [`Assets/_Project/UI/Runtime/JCW_Script/Views/PartyMemberCard.cs`](Assets/_Project/UI/Runtime/JCW_Script/Views/PartyMemberCard.cs) | 52 / 54 | 96% |
| [`Assets/_Project/UI/Runtime/JCW_Script/Views/PlayerStatusBar.cs`](Assets/_Project/UI/Runtime/JCW_Script/Views/PlayerStatusBar.cs) | 60 / 60 | 100% |
| [`Assets/_Project/UI/Runtime/JCW_Script/Views/SkillCutIn.cs`](Assets/_Project/UI/Runtime/JCW_Script/Views/SkillCutIn.cs) | 160 / 160 | 100% |
| [`Assets/_Project/UI/Runtime/JCW_Script/Views/SkillSlot.cs`](Assets/_Project/UI/Runtime/JCW_Script/Views/SkillSlot.cs) | 87 / 87 | 100% |
| [`Assets/_Project/UI/Runtime/JCW_Script/Views/StatusEffectDisplay.cs`](Assets/_Project/UI/Runtime/JCW_Script/Views/StatusEffectDisplay.cs) | 42 / 42 | 100% |
| [`Assets/_Project/UI/Runtime/JCW_Script/Views/StatusEffectIcon.cs`](Assets/_Project/UI/Runtime/JCW_Script/Views/StatusEffectIcon.cs) | 40 / 40 | 100% |
| [`Assets/_Project/UI/Runtime/JCW_Script/Views/StatusEffectTracker.cs`](Assets/_Project/UI/Runtime/JCW_Script/Views/StatusEffectTracker.cs) | 63 / 63 | 100% |
| [`Assets/_Project/UI/Runtime/JCW_Script/Views/UltimateGaugeView.cs`](Assets/_Project/UI/Runtime/JCW_Script/Views/UltimateGaugeView.cs) | 77 / 77 | 100% |
| [`Assets/_Project/UI/Runtime/JCW_Script/Views/WeaponCutInSelector.cs`](Assets/_Project/UI/Runtime/JCW_Script/Views/WeaponCutInSelector.cs) | 41 / 41 | 100% |
| [`Assets/_Project/UI/Runtime/Lobby/DungeonSignBoard.cs`](Assets/_Project/UI/Runtime/Lobby/DungeonSignBoard.cs) | 27 / 27 | 100% |
| [`Assets/_Project/UI/Runtime/Lobby/NpcAppearance.cs`](Assets/_Project/UI/Runtime/Lobby/NpcAppearance.cs) | 69 / 69 | 100% |
| [`Assets/_Project/UI/Runtime/Lobby/NpcDialogue.cs`](Assets/_Project/UI/Runtime/Lobby/NpcDialogue.cs) | 95 / 95 | 100% |
| [`Assets/_Project/UI/Runtime/Lobby/WorldLabel.cs`](Assets/_Project/UI/Runtime/Lobby/WorldLabel.cs) | 76 / 76 | 100% |
| [`Assets/_Project/UI/Runtime/Onboarding/OnboardingChecklistPanel.cs`](Assets/_Project/UI/Runtime/Onboarding/OnboardingChecklistPanel.cs) | 94 / 94 | 100% |
| [`Assets/_Project/UI/Runtime/Onboarding/OnboardingEntry.cs`](Assets/_Project/UI/Runtime/Onboarding/OnboardingEntry.cs) | 72 / 72 | 100% |
| [`Assets/_Project/UI/Runtime/Onboarding/OnboardingItemView.cs`](Assets/_Project/UI/Runtime/Onboarding/OnboardingItemView.cs) | 63 / 63 | 100% |
| [`Assets/_Project/UI/Runtime/Onboarding/OnboardingPortalGate.cs`](Assets/_Project/UI/Runtime/Onboarding/OnboardingPortalGate.cs) | 42 / 42 | 100% |
| [`Assets/_Project/World/Runtime/FlickerLight.cs`](Assets/_Project/World/Runtime/FlickerLight.cs) | 60 / 60 | 100% |
| [`Assets/_Project/World/Runtime/Materials/CubeWorldSmoke.shader`](Assets/_Project/World/Runtime/Materials/CubeWorldSmoke.shader) | 201 / 201 | 100% |
| [`Assets/_Project/World/Runtime/Materials/CubeWorldSoftParticle.shader`](Assets/_Project/World/Runtime/Materials/CubeWorldSoftParticle.shader) | 104 / 104 | 100% |
| [`Assets/_Project/World/Runtime/Materials/CubeWorldWater.shader`](Assets/_Project/World/Runtime/Materials/CubeWorldWater.shader) | 344 / 344 | 100% |
| [`Assets/_Project/World/Runtime/Scripts/LobbyAmbience.cs`](Assets/_Project/World/Runtime/Scripts/LobbyAmbience.cs) | 487 / 487 | 100% |

## 수정에 참여한 파일

| 파일 | 내 줄 / 전체 | 비율 |
|---|---|---|
| [`Assets/_Project/Combat/Runtime/Weapon/Weapon_WoodShield.cs`](Assets/_Project/Combat/Runtime/Weapon/Weapon_WoodShield.cs) | 337 / 490 | 69% |
| [`Assets/_Project/Combat/Runtime/Player/PlayerStateMachine.cs`](Assets/_Project/Combat/Runtime/Player/PlayerStateMachine.cs) | 197 / 505 | 39% |
| [`Assets/_Project/Combat/Runtime/Weapon/Weapon_HealingStaff.cs`](Assets/_Project/Combat/Runtime/Weapon/Weapon_HealingStaff.cs) | 112 / 280 | 40% |
| [`Assets/_Project/UI/Runtime/DamageNumber/DamageNumber.cs`](Assets/_Project/UI/Runtime/DamageNumber/DamageNumber.cs) | 94 / 121 | 78% |
| [`Assets/_Project/Combat/Runtime/Weapon/IWeapon.cs`](Assets/_Project/Combat/Runtime/Weapon/IWeapon.cs) | 72 / 101 | 71% |
| [`Assets/_Project/UI/Runtime/DamageNumber/DamageNumberPresenter.cs`](Assets/_Project/UI/Runtime/DamageNumber/DamageNumberPresenter.cs) | 63 / 84 | 75% |
| [`Assets/_Project/DataBases/Runtime/DatabaseBootstrap.cs`](Assets/_Project/DataBases/Runtime/DatabaseBootstrap.cs) | 55 / 183 | 30% |
| [`Assets/_Project/Combat/Runtime/Weapon/EffectPool.cs`](Assets/_Project/Combat/Runtime/Weapon/EffectPool.cs) | 54 / 82 | 66% |
| [`Assets/_Project/Combat/Runtime/Weapon/ParticleGroup.cs`](Assets/_Project/Combat/Runtime/Weapon/ParticleGroup.cs) | 52 / 63 | 83% |
| [`Assets/_Project/Core/Runtime/DataBases/Records/AttackRecord.cs`](Assets/_Project/Core/Runtime/DataBases/Records/AttackRecord.cs) | 47 / 100 | 47% |
| [`Assets/_Project/Combat/Runtime/Player/States/SkillState.cs`](Assets/_Project/Combat/Runtime/Player/States/SkillState.cs) | 45 / 63 | 71% |
| [`Assets/_Project/Combat/Runtime/Player/States/AttackingState.cs`](Assets/_Project/Combat/Runtime/Player/States/AttackingState.cs) | 42 / 62 | 68% |
| [`Assets/_Project/Combat/Runtime/Player/PlayerLifeCycle.cs`](Assets/_Project/Combat/Runtime/Player/PlayerLifeCycle.cs) | 37 / 108 | 34% |
| [`Assets/_Project/Core/Runtime/GameplayEvents.cs`](Assets/_Project/Core/Runtime/GameplayEvents.cs) | 33 / 280 | 12% |
| [`Assets/_Project/Combat/Runtime/Weapon/BasicRangeWeapon.cs`](Assets/_Project/Combat/Runtime/Weapon/BasicRangeWeapon.cs) | 32 / 157 | 20% |
| [`Assets/_Project/Combat/Runtime/CombatSetup.cs`](Assets/_Project/Combat/Runtime/CombatSetup.cs) | 23 / 78 | 29% |
| [`Assets/_Project/Combat/Runtime/Player/States/UltimateState.cs`](Assets/_Project/Combat/Runtime/Player/States/UltimateState.cs) | 22 / 40 | 55% |
| [`Assets/_Project/UI/Runtime/InventoryToggleInput.cs`](Assets/_Project/UI/Runtime/InventoryToggleInput.cs) | 22 / 94 | 23% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossMotor.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossMotor.cs) | 21 / 116 | 18% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/PatternDefinition.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/PatternDefinition.cs) | 17 / 66 | 26% |
| [`Assets/_Project/Combat/Runtime/Player/States/PlayerStateBase.cs`](Assets/_Project/Combat/Runtime/Player/States/PlayerStateBase.cs) | 17 / 56 | 30% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossView.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossView.cs) | 15 / 79 | 19% |
| [`Assets/_Project/Combat/Runtime/Weapon/WeaponContainer.cs`](Assets/_Project/Combat/Runtime/Weapon/WeaponContainer.cs) | 12 / 35 | 34% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossFSM/BossChaseState.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossFSM/BossChaseState.cs) | 11 / 53 | 21% |
| [`Assets/_Project/Core/Runtime/DataBases/Records/ItemRecord.cs`](Assets/_Project/Core/Runtime/DataBases/Records/ItemRecord.cs) | 10 / 41 | 24% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossDefinition.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossDefinition.cs) | 7 / 106 | 7% |
| [`Assets/_Project/Combat/Runtime/Player/PlayerBuffable.cs`](Assets/_Project/Combat/Runtime/Player/PlayerBuffable.cs) | 7 / 149 | 5% |
| [`BuildTools/CI/check_module_boundaries.py`](BuildTools/CI/check_module_boundaries.py) | 6 / 85 | 7% |
| [`Assets/_Project/BossContent/02. Scripts/Boss/BossPattern/BossPatternBase.cs`](Assets/_Project/BossContent/02.%20Scripts/Boss/BossPattern/BossPatternBase.cs) | 5 / 178 | 3% |

## 내 커밋 기록

<details><summary>커밋 350개 펼치기</summary>

| 날짜 | 커밋 | 메시지 |
|---|---|---|
| 2026-09-10 | `871e690` | 플레이어 사운드 배선 |
| 2026-09-08 | `309accf` | 온보딩 씬 정상화 |
| 2026-09-08 | `f94db27` | 온도핑 "궁극기를 사용하세요(TAP)" 를 추가 |
| 2026-09-07 | `2b1845e` | 이미지 교체 |
| 2026-09-07 | `af13828` | 더퍼윌한테 죽으면 무한루프 걸리는 문제 픽스 |
| 2026-09-07 | `4e370dd` | 빠진 프리팹 |
| 2026-09-07 | `3221eef` | 캐릭터 프로필 이미지 교체 |
| 2026-09-06 | `73cb450` | 더퍼윌 싱크 조정 거의 끝남(TFW콤보패턴의 부모인 BossPatternBase가 바꼈던데 충돌날수도?) |
| 2026-09-06 | `c4f3cd9` | 스폰연출 개선,페이즈 전환 이펙트 추가 |
| 2026-09-05 | `452853c` | 스폰 연출 |
| 2026-09-05 | `11fe7d2` | 더퍼윌 전조 애니메이션 + 싱크조정중 |
| 2026-09-05 | `6bfcdd7` | 보스 배속적용 |
| 2026-09-05 | `a87a59c` | 더퍼윌 코드 리팩토링 |
| 2026-09-05 | `5ef84b3` | 더퍼윌 싱크 조정중 |
| 2026-09-04 | `8d0fdc8` | 더퍼윌 작업중 |
| 2026-09-04 | `71899ec` | 플레이어 스턴 애니메이션 정상화 |
| 2026-09-03 | `da4c8dd` | 더퍼윌 싱크 작업중 |
| 2026-09-03 | `6a91175` | 더퍼윌 공중에서 죽는 문제 픽스 |
| 2026-09-03 | `d5dd2bb` | 채팅창 정상화 |
| 2026-09-03 | `25ae2e2` | [캐릭터 프로필 레이아웃 깨지는거 정상화] |
| 2026-09-03 | `e33d772` | [채팅 이미지 작업중] |
| 2026-09-03 | `8d9e1ed` | 강화창 ui |
| 2026-09-03 | `cb90646` | 더퍼윌 보상상자,로비로 가는 포탈 |
| 2026-09-03 | `17c93bc` | [npc 위치 정상화(부모가 아닌 자식을 옮겨버림)] |
| 2026-09-03 | `4254597` | 로비씬 정상화, 로딩 정상화 |
| 2026-09-03 | `847c127` | AllPlayerReviveHandler |
| 2026-09-03 | `c928fa4` | 딜미터기 |
| 2026-09-02 | `2909336` | 부트스트랩 캔버스 업데이트 |
| 2026-09-02 | `ab627aa` | 힐러 힐 정상화 |
| 2026-09-02 | `1f4659c` | 부트스트랩 로딩바 정상화 |
| 2026-09-02 | `e3b16a2` | 보스싱크 1차 |
| 2026-09-02 | `89dfa0d` | 온보딩 4체인 트루, 씬넘어갈때 체인 ui 디스폰 |
| 2026-09-02 | `b436e0d` | 보스 세이크 정상화 |
| 2026-08-31 | `fe80168` | [보스 첫 소환시 딜레이] |
| 2026-08-31 | `f1588eb` | 경계검사 |
| 2026-08-30 | `fe61555` | 카메라 휠 업다운 |
| 2026-08-30 | `e088575` | 힐러 버그 정상화 |
| 2026-08-29 | `eb47437` | 로비 꾸미기(반딧불이,굴뚝연기) |
| 2026-08-29 | `b924f0c` | 게임씬 라이팅 |
| 2026-08-28 | `d36fead` | npc 옷바꿈 (최적화 해야함) |
| 2026-08-28 | `4df9fca` | 씬 빌드세팅 |
| 2026-08-28 | `aa8aeab` | 말풍선과 최초의윌더 채팅안되던 문제 수정 |
| 2026-08-28 | `dc63ef6` | npc 추가 및 대화 추가 |
| 2026-08-28 | `a78bd7f` | 온보딩 완전 싱글모드(호스트모드일때 진입불가) |
| 2026-08-28 | `053c4e1` | 온보딩씬 lod 그룹 제거 |
| 2026-08-27 | `c2f229a` | 멥 작업중 |
| 2026-08-27 | `f502e3e` | 보스 싱크 작업중 |
| 2026-08-27 | `0846692` | 더 퍼스트 윌더 맵 리메이크 |
| 2026-08-27 | `134da42` | 로비씬 맵 만듬 |
| 2026-08-27 | `be5e358` | 더퍼스트윌더 맵 추가 |
| 2026-08-27 | `6d3d29b` | 누락된 메타파일 |
| 2026-08-27 | `23fd6e7` | 온보딩 맵과 라이팅 |
| 2026-08-26 | `b1eecfe` | 포털 추가 |
| 2026-08-26 | `c08b1b9` | 온보딩 리팩토링(프레임워크 ex:DungeonOnboardingProvider를 새로 짜서 던전용 Course 에셋을 꽂습니다) |
| 2026-08-26 | `a4e240e` | 온보딩 |
| 2026-08-26 | `2f11eef` | [보스 카메라진동] |
| 2026-08-26 | `c5ce16e` | 레이저 이팩트 |
| 2026-08-26 | `13a5d58` | 오러 작업 |
| 2026-08-26 | `639a088` | 이펙트 튜너, 오프셋 작업중 |
| 2026-08-25 | `7bb9662` | 애니 제자리 버그 수정 |
| 2026-08-25 | `5a4415a` | 오러 이펙트 |
| 2026-08-25 | `0a07617` | 애니메이션 작업중 |
| 2026-08-25 | `8bb6ac7` | 보스 이펙트 |
| 2026-08-24 | `4980e9b` | 무기에셋,애니메이션 작업중 |
| 2026-08-24 | `f1fe0b3` | [TFW셋업 에디터 툴] |
| 2026-08-24 | `7fea3ea` | [코드]2페이즈 작업중 |
| 2026-08-23 | `0e12e22` | 2페이즈 코드 (검과방패) |
| 2026-08-23 | `de8891b` | 보스 1페이즈 구조 끝(전진거리 루트모션의 곡선을 재현할려고 어쩔수 없이 bossMotor 수정함,기존의 보스는 바뀐거없음,한글 파일들 영어로 수정할예정) |
| 2026-08-22 | `d8eb9fd` | [보스 패턴1 작업중, 클립 이름 영어로 수정예정] |
| 2026-08-22 | `d4b34e6` | 최초의 윌더 작업중(코드,프리팹,so,보스시트) |
| 2026-08-22 | `e39fac6` | TheFirstWielder 매시 에셋 |
| 2026-08-21 | `e01fa2a` | [캐릭터프로필]이미지는 나중에 교체할거임 그냥 참고 하셈 |
| 2026-08-21 | `69e8828` | 프로필 작업중 |
| 2026-08-21 | `9f34f16` | 프로필 이미지 작업중 |
| 2026-08-20 | `f4ae65a` | 이미지 뿌직 작업중 |
| 2026-08-20 | `12d1b13` | [0.0001차 밸런스 수정]시트수정 |
| 2026-08-20 | `8fcc241` | [프로필]토글형 스킬설명 뺏음,상시 텍스트,프로필 이미지 에셋 추가 |
| 2026-08-19 | `8e9f21b` | [프로필,코드,프리팹]플레이어프로필 목업 |
| 2026-08-18 | `172d571` | 가드레일 |
| 2026-08-17 | `6501d87` | 이펙트 왜곡잔상 셰이더그래프 |
| 2026-08-17 | `4c5e1e7` | [so]쉴드 웨폰 막기 루프애니메이션 정상화 |
| 2026-08-17 | `92fb2fe` | [웨폰,코드]히트이펙트 |
| 2026-08-16 | `8366bbe` | [에셋]히트이펙트 |
| 2026-08-16 | `b556b86` | [에셋]반격 이펙트 |
| 2026-08-16 | `8dab423` | [플레이어,웨폰,코드]죽은코드 삭제(TryDashTransition,DashCost),중복 로직 통합(사망·CC 시 전진(lunge)을 끊는 코드) |
| 2026-08-16 | `c33847b` | [웨폰,코드,에셋]반격 애니메이션 추가,반격 관련 버그 픽스 |
| 2026-08-16 | `691bf58` | [웨폰,코드]쉴드 웨폰 조작감 개선(방패올리기 선입력) |
| 2026-08-16 | `947de87` | [so,툴]전 캐릭 조작감 대폭개선(애니매이션 클립의 끝을 자른 만큼 거리도 버림,눈에 보이는 전진거리랑 애니메이션 전진거리랑 일치) |
| 2026-08-16 | `acf380d` | [웨폰,코드]쉴드웨폰 특수기 추가(막기 성공시 특수좌클릭(반격) 활성화) |
| 2026-08-16 | `e53b4e6` | [플레이어,웨폰,코드]쉴드 웨폰에 방패막기로 어택 후딜캔슬 추가 |
| 2026-08-16 | `3207983` | 에셋 폴더이동 |
| 2026-08-15 | `45bee7f` | [플레이어,코드]대시중 cc면역 |
| 2026-08-15 | `8e2fb06` | [웨폰,코드]타격으로 스테미너 회복 추가 |
| 2026-08-15 | `4697674` | [애셋]대시 이펙트 |
| 2026-08-15 | `4aab7bb` | [웨폰,코드]쉴드웨폰의 skill도(방패막기) 대시랑 같은 자원소모(스테미너) |
| 2026-08-15 | `08128f7` | [플레이어,웨폰,코드]대시코드 리팩토링중 |
| 2026-08-15 | `7ebf638` | [so,에셋]대시 애니메이션 작업중 |
| 2026-08-15 | `c4b5c05` | [ui,코드,프리팹]스테미너bar 배선및 연결 |
| 2026-08-15 | `ff51541` | [툴]이펙트 오프셋에 백대시 편집기능 추가 |
| 2026-08-15 | `5b3a1ca` | [애니메이션,씬]대시,백대시 애니메이션 에셋 추가및 베이스 컨트롤러 배선 |
| 2026-08-15 | `7d3e298` | [so,씬]백대시 시트행 추가 |
| 2026-08-15 | `9136e4e` | [플레이어,코드]백대시 추가 |
| 2026-08-14 | `930f5fd` | [so,씬]구글시트에 대시행 추가,부트스트랩 업데이트 |
| 2026-08-14 | `5a66a0e` | 애니메이션 이펙트 툴에 대시 추가 |
| 2026-08-14 | `1a92bcc` | 대시 컨트롤러 추가 |
| 2026-08-14 | `25b7f7d` | [네트워크,코드,인풋액션]대시 인풋액션 추가 |
| 2026-08-14 | `7ace448` | [코어,코드]인풋 대시 추가 및 ui배선을 위한 이벤트추가 |
| 2026-08-14 | `17586bf` | [Weapon,코드]대시 추가 |
| 2026-08-14 | `4d12435` | [States,코드] 대시 추가 |
| 2026-08-14 | `ed958e8` | [ui,이미지,셰이더그래프]얼티밋 이펙트 마스크 추가 |
| 2026-08-14 | `fff55d6` | [ui이미지,프리팹]로컬플레이어 프레임 이미지 정상화 |
| 2026-08-12 | `529e787` | [UI코드,프리팹]플레이어와 보스 체력,데미지에 천 단위 콤마(,) 적용 |
| 2026-08-11 | `eefece6` | [ui,코드,프리팹]데미지넘버 정상화 "영수증"(두트윈 적용,폰트적용,컬러적용,폰트정렬 이상한거 정상화,폰트가 보스에 파묻혔던걸 이제 오버레이로 해놔서 무조건 앞으로옴) |
| 2026-08-11 | `4c47136` | [ui에셋]데미지폰트 에셋 |
| 2026-08-09 | `dfa9941` | [ui이미지]버서커웨폰5004 이미지 |
| 2026-08-09 | `63a4d28` | [ui,코어,네트워크 코드,프리팹]네트워크 채팅기능 추가 |
| 2026-08-09 | `b9e4a2a` | [ui이미지,프리팹]가드 이미지 2종 |
| 2026-08-09 | `3fa918f` | [ui코드]체인배너와 가드배너를 스프라이트 교체방식이 아닌 토글로 미리 올려두는 방식 |
| 2026-08-09 | `39ce101` | [ui,코어 코드]쉴드 웨폰 가드 성공시 자신의 화면에 가드 성공 ui 연출 |
| 2026-08-09 | `83a5028` | [ui코드]기존의 체인게이지를 체인배너에 몰아넣음.체인단계수 올라갈때마다 체인스프라이트 교체 |
| 2026-08-09 | `8009e2f` | [ui이미지]체인 게이지,체인 단계 이미지 추가 |
| 2026-08-09 | `735fbb4` | [컴벳코드]풀체인 발동되자마자 체인 비우는 문제 해결 (이제 ui에 풀체인 4명 반영됨) |
| 2026-08-09 | `e55cecb` | [ui프리팹]파티원 체인컷인 작업중 |
| 2026-08-08 | `5a2e0c3` | [ui프리팹,이미지]파티원 컷인 스택 |
| 2026-08-08 | `56d38a6` | [ui코드]파티원 컷인 우측하단에 스택으로 쌓임(체인) |
| 2026-08-08 | `67b3865` | [ui이미지]보스 hp 이미지 변경 |
| 2026-08-05 | `3cfbf5c` | [UI 로컬플레이어 이미지 교체 작업중] |
| 2026-08-05 | `847de89` | [ui,세이더]각종 아트스타일 ui세이더 테스트중. |
| 2026-08-05 | `7b51293` | [플레이어 프리팹]플레이어 장비 이름 세분화(웨폰종류 따라가게) |
| 2026-08-04 | `4b5c50a` | [파티맴버 프리팹,머테리얼]파티맴버카드에 스킬컷인 배선 |
| 2026-08-04 | `22a6bae` | [파티맴버 코드]파티맴버카드에도 파티원의 개별컷인 추가 |
| 2026-08-04 | `c8003ad` | [ui컷인 위치 정상화] |
| 2026-08-03 | `99ec20e` | [ui 컷인 슬라이드형태로 변경, 이미지의 양끝에 알파값 반투명을 주기위한 셰이더머터리얼 추가] |
| 2026-08-03 | `87ecb01` | [체인컷인 작업중] |
| 2026-08-02 | `8920108` | [공격력 증가 반영을 위한 PlayerCombatStats 추가,(현재 서로 다른 공격력 버프 출처가 서로를 덮어쓰고 제거하는 문제있음 추후 수정)] |
| 2026-08-02 | `891c9a5` | [힐러,방패 풀체인 추가,임시로 공격력 증가로 넣음(하지만 실제 공격력증가 구독자가 없어서 실제 공격력은 반영안됨)] |
| 2026-08-02 | `21287e3` | [버서커랑 활잽이도 풀체인 이펙트 추가] |
| 2026-08-02 | `8169d54` | [풀체인 이펙트,(일단 전사만),풀체인 테스트용으로 전사 어택레코드 얼티밋 쿨타임 0초,테스트끝나면 까먹지말고 시트 돌려놓기] |
| 2026-08-02 | `c50ae84` | [부트스트랩 업데이트(버서커 풀체인레코드)] |
| 2026-08-02 | `12c6648` | [버서커도 딜러 풀체인] |
| 2026-08-02 | `395a81d` | [풀체인 발동시 이펙트는 체인스타터의 궁극기 타격지점 기준(보스 움직여도 앵커따라감). 궁극기를 맞춰야만 체인연결됨. 단 힐러는 공격판정이 없어서 발동시전으로 예외둠,(체인스타터는 현재 딜러만 구현됨)] |
| 2026-08-02 | `0ea5972` | [탱커 막기 추가(저스트 가드 완전무효,일반홀드막기 50퍼 감소,전방만 판정)] |
| 2026-08-01 | `1958ab6` | [부트스트랩 업데이트] |
| 2026-08-01 | `9b9ca2e` | [풀체인 배치,시트 동기화] |
| 2026-08-01 | `6bc73d1` | Revert "[시트에 풀체인 배치,시트 동기화]" |
| 2026-08-01 | `9025b95` | [딜러 풀체인 작업중] |
| 2026-08-01 | `2c0ffbf` | [시트에 풀체인 배치,시트 동기화] |
| 2026-08-01 | `59fc34c` | [로비에 도끼무기 추가] |
| 2026-08-01 | `3e79a34` | [버서커 레코드 조정, 코스튬 추가] |
| 2026-08-01 | `42a7615` | [신캐 추가 완 버서커] |
| 2026-08-01 | `4b5099f` | [버서커 무기추가중] |
| 2026-08-01 | `970bc36` | [버서커 허공에 때려도 공속중첩되는 문제 픽스] |
| 2026-07-31 | `34b6da0` | [ui컷인 개편중] |
| 2026-07-31 | `1211531` | [ui 스킬컷인에 체인마무리컷인 삭제] |
| 2026-07-31 | `e66f4c6` | [풀 용량 늘림 최대 99개] |
| 2026-07-31 | `573c38f` | [버서커 작업중] |
| 2026-07-30 | `24837f7` | [버서커 웨폰 추가중2] |
| 2026-07-30 | `9309bee` | [버서커웨폰 추가중] |
| 2026-07-30 | `3411b04` | [힐러 평타 속도 정상화] |
| 2026-07-30 | `5c1501c` | [보스hp바, 플레이어 닉네임 레이캐스트 해제] |
| 2026-07-30 | `c62b0b9` | [이펙트 작업 다되감] |
| 2026-07-29 | `c199c7d` | [이펙트,본 로테이션 도라에몽 만능툴] |
| 2026-07-29 | `c9ea2f3` | [이펙트풀 리팩토리중] |
| 2026-07-29 | `090f0d4` | [💩전사 이펙트 추가 어택 레코드 💩작업중💩] |
| 2026-07-29 | `7361f66` | Revert "[히트스탑 연출]" |
| 2026-07-29 | `e66874f` | Revert "풀 재사용 시 남의 이펙트를 멈출 위험과 기존 다단히트 데이터 유실 가능성을 막고 이름과 구조를 더 명확하게 정리" |
| 2026-07-29 | `0650076` | 풀 재사용 시 남의 이펙트를 멈출 위험과 기존 다단히트 데이터 유실 가능성을 막고 이름과 구조를 더 명확하게 정리 |
| 2026-07-29 | `7e76bfc` | [히트스탑 연출] |
| 2026-07-28 | `18efeeb` | [플레이어 카메라 각도 테스트중] |
| 2026-07-28 | `b66c3bf` | [플레이어 조작감 개선, 차징중에는 마우스 고정, 차징 시작과 끝은 마우스 에임대로] |
| 2026-07-28 | `a4eaac9` | [레코드 작업중] |
| 2026-07-28 | `3154c8e` | [전용 툴 최신화] |
| 2026-07-28 | `7ff8c13` | [워폰베이스 레코드 애니메이션 이벤트 데이터화] |
| 2026-07-27 | `da5511e` | [전진 작업중] |
| 2026-07-27 | `851beb5` | [뿌직(전진기)] |
| 2026-07-27 | `e36e818` | [컴벳 에디터 툴(이펙트 오프셋,전진거리] |
| 2026-07-27 | `6eb1fe9` | [웨폰 이펙트위치 버그 수정,어택 레코드 필드 추가] |
| 2026-07-26 | `79f568a` | [스킬컷인 리팩토링] |
| 2026-07-26 | `8e72c32` | [체인컷인 이미지 변경,프리팹 배선 완] |
| 2026-07-26 | `1fc9aa9` | [컷인 배선,위치조절 하기] |
| 2026-07-26 | `9dfa798` | [Chain Cutin (수정예정 or 삭제예정)] |
| 2026-07-26 | `ecf3cb5` | [Cutin 이미지 4종(수정 예정)] |
| 2026-07-26 | `9de4097` | [일단 싸고 내일하기] |
| 2026-07-25 | `86941be` | [게임씬 Light Baking,기존 씬 복사해서 테스트중] |
| 2026-07-25 | `6124cd7` | [스킬컷인 테스트] |
| 2026-07-24 | `26c9904` | [도끼 애니메이션 작업중] |
| 2026-07-24 | `8ec0317` | 찐빠나는 부분 리터칭 |
| 2026-07-23 | `da8760c` | [사심가득 로딩스크린 이미지] |
| 2026-07-23 | `4ba0faa` | [로비에 로비블륨추가,캐릭터 헤어 조정] |
| 2026-07-23 | `e6a669c` | [UI 플레이어 쿨타임 추가] |
| 2026-07-23 | `5530533` | [플레이어 카메라 컨트롤러, 렌즈값(fov) 필드 추가] |
| 2026-07-22 | `e188d86` | [플레이어 무기 교체시 아머,헤어도 교체] |
| 2026-07-22 | `1fbdf44` | [플레이어 무기 교체] |
| 2026-07-22 | `6b7f7fd` | [체인 지속시간 윈도우틱으로 받던거 윈도우 세컨즈로 수정,체인 지속시간 배선] |
| 2026-07-22 | `87c81ff` | [메인 카메라 프리팹 post 중복으로 켜져있는거 정상화] |
| 2026-07-22 | `2bdff48` | [ui 플레이어 hp바 살짝 어긋난거 수정] |
| 2026-07-22 | `085a211` | [lastChainContributorId 다 걷어냄,이제 체인 잘나옴] |
| 2026-07-22 | `fbf9e62` | [UI: 힐러 버프 아이콘 추가(데미지 증가)] |
| 2026-07-21 | `7b01aab` | [테스트용 Volume] |
| 2026-07-21 | `2c5b4a7` | [타이틀 정상화,PressAnyKeyPrompt 추가] |
| 2026-07-21 | `046264c` | [탱커 쉴드 이펙트 추가] |
| 2026-07-21 | `3075879` | [탱커 쉴드 애니메이션] |
| 2026-07-21 | `809da3c` | [아처 이펙트] |
| 2026-07-20 | `2785ba1` | [카탈로그 빠진거] |
| 2026-07-20 | `4d2a2c1` | [UI 공격력 증가 아이콘] |
| 2026-07-20 | `8a61e31` | [Animator 아처] |
| 2026-07-20 | `ac155a4` | [힐러 이펙트] |
| 2026-07-20 | `73a71cb` | [Animator : 힐러_스태프 애니메이션] |
| 2026-07-20 | `3767397` | [ui: 체인 ux 급한대로 일단 pr, 나중에 수정 예정] |
| 2026-07-19 | `58856d2` | [UI:파티원 카드 얼티밋 이펙트 위치조정] |
| 2026-07-19 | `ede59f5` | [UI 디버프 테스트용 코드 (테스트 한 뒤 잊지말고 꼭 지울것)] |
| 2026-07-19 | `2f09f73` | [UI: 디버프 카탈로그에 이미지 연결] |
| 2026-07-19 | `311062b` | [UI 디버프 이미지 5종 추가 (스턴,슬로우,어택다운,디펜스 다운,도트템)] |
| 2026-07-18 | `79c5075` | [씬 업데이트] |
| 2026-07-17 | `af57493` | [UX:체인스타터,2체인,3체인 연출용 머테리얼] |
| 2026-07-17 | `be7b50b` | [UX:체인 연출용 셰이더 그래프를 화면상에 그리는 용도로 URP Renderer에 추가] |
| 2026-07-17 | `de2781a` | [UX 체인 연출 셰이더그래프] |
| 2026-07-17 | `a1cef4e` | [UI:체인 연출 배선] |
| 2026-07-16 | `6582b10` | [UI 프리팹 컴포넌트 연결] |
| 2026-07-16 | `e7695ab` | [ui: 작업중] |
| 2026-07-16 | `2e8183f` | [DataBases] |
| 2026-07-16 | `186119b` | [UI:Player 디스폰, Boss 스폰,디스폰 이벤트 추가, 체인 배선 연결] |
| 2026-07-15 | `1431dc7` | [ui 버프,디버프 작업중] |
| 2026-07-14 | `6d408af` | [ui 테스트 씬 업데이트,이미지 업데이트] |
| 2026-07-14 | `f005a85` | [ui작업중 머지할게요] |
| 2026-07-14 | `a06c74a` | [UI:HudOrchestrator 핸들러 private로 변경,PlayerUiPanel null 정책 통일] |
| 2026-07-14 | `ec10430` | [UI: 클래스 변경,클래스 마다 얼티밋 이펙트 추가] |
| 2026-07-13 | `0aab90b` | [ui:체인 게이지 프리팹 작업중] |
| 2026-07-13 | `37b21d4` | [UI 씬업데이트] |
| 2026-07-13 | `27d4cfe` | [UI HUD 오버라이드] |
| 2026-07-13 | `c27bdf3` | [UI:보스 Hpbar 교체, 플레이어랑 보스 Hpbar에 text로 체력표기,플레이어 프레임 변경] |
| 2026-07-13 | `84745b2` | [UI 작업중] |
| 2026-07-13 | `d9e88b3` | [UI 이미지 퀄리티 조정] |
| 2026-07-12 | `2492413` | [UI:Header 가독성 개선] |
| 2026-07-12 | `9f0012c` | [UI: 변수명 정상화] |
| 2026-07-12 | `17e3a25` | [UI: StatusBar 분리로 인한 슬롯타입 변경으로 프리팹 컴포넌트 재연결] |
| 2026-07-12 | `78450de` | [UI:기존에 통합되어있던 StatusBar 를 Player와 Boss로 분리] |
| 2026-07-11 | `331759e` | [ui이미지 정리] |
| 2026-07-10 | `c20996c` | [fix:TTA 노란경고 정상화] |
| 2026-07-10 | `60cd4db` | [UI: GameHUD 계층구조 정상화및 스텟바 머테리얼셰이더 속도 조절] |
| 2026-07-10 | `5d8ce26` | [UI:파티멤버 코드에 얼티밋 이펙트 추가] |
| 2026-07-10 | `ba60b5e` | [UI:User1 - Local Player로 개명, Local Player 계층 정리] |
| 2026-07-10 | `16c2aee` | [UI:마우스 좌,우클릭 이미지] |
| 2026-07-10 | `fdadc9f` | [유저 hp바 연동, 파티원 프레임 추가] |
| 2026-07-10 | `8f7b9c9` | [ui 메인카메라 프리팹 오디오 리스너 삭제,psd 파일 삭제, 카메라 메타파일 추가] |
| 2026-07-10 | `0308fa7` | [파티원 ui 교체] |
| 2026-07-10 | `2475ce5` | [ui 카메라 프리팹 안넣으면 ui안보임] |
| 2026-07-10 | `4fdd036` | [ui 진행도 절반 정도 일단 PR] |
| 2026-07-10 | `471aab2` | [UI 작업중] |
| 2026-07-09 | `9013212` | [집가자] |
| 2026-07-09 | `357b195` | ui:cs작업중. [경험치,얼티밋 이펙트] |
| 2026-07-09 | `ec9f235` | ui작업중 |
| 2026-07-09 | `082ac9c` | ui작업중 |
| 2026-07-09 | `13d06c9` | [안쓰는거 정리] |
| 2026-07-09 | `f5a0e0c` | [UI 작업중] |
| 2026-07-08 | `fe6d083` | [ui: 임시로 bossId를 ui에 연결] |
| 2026-07-08 | `659ee34` | [파티원 HP바 작업중] |
| 2026-07-07 | `4a3730c` | [UI이미지 작업중] |
| 2026-07-07 | `896b4d2` | 캔버스 리네임 |
| 2026-07-07 | `b2ed680` | UI:fix 파티원 스폰안되는문제 |
| 2026-07-07 | `5dbaff2` | 캔버스 프리팹 최신화 |
| 2026-07-07 | `940901d` | [UI: 프리팹] |
| 2026-07-07 | `39cec82` | [UI: HUD를 3계층(Orchestrator → 패널 → 재사용 뷰)** 구조로 구현] |
| 2026-07-07 | `3413b4e` | [DOTween 임포트] |
| 2026-07-07 | `09df935` | 프리팹 폴더 이동 |
| 2026-07-07 | `2f2e130` | [씬업데이트] |
| 2026-07-07 | `e754f62` | [boss ui 계층 세분화] |
| 2026-07-07 | `c567c07` | [메뉴 ui 계층 세분화] |
| 2026-07-07 | `320c0a9` | [파티원 ui 계층 세분화] |
| 2026-07-07 | `107f36b` | [User Ui 계층 세분화] |
| 2026-07-06 | `68667d2` | [캔버스 프리팹] |
| 2026-07-06 | `ea5193d` | [UI:프로토타입] |
| 2026-07-06 | `0492f90` | [Title:다음 씬 전환] |
| 2026-07-06 | `95ddd9b` | fix:터레인 파일명,워터 이름,씬업데이트, |
| 2026-07-06 | `c2c0737` | probuilder |
| 2026-07-06 | `80bbaf8` | 씬 업데이트 |
| 2026-07-06 | `40701bd` | fix 터레인 |
| 2026-07-06 | `19e26f0` | [Title: 스카이 박스 3종(아침,노을,밤) |
| 2026-07-06 | `22e323c` | [안쓰는 셰이더 정리] |
| 2026-07-06 | `e02ca5c` | [물 셰이더 에셋 정리] |
| 2026-07-06 | `1c1d09b` | [Title:터레인 추가] |
| 2026-07-06 | `d4e404e` | [Title:에셋 경로 이동] |
| 2026-07-05 | `a728da8` | [Title:앉기] |
| 2026-07-05 | `326ab01` | [Title:앉기 모션] |
| 2026-07-05 | `8e58d8d` | [Title: 타이틀 프로토타입] |
| 2026-07-05 | `ec99477` | [Title:물 셰이더] |
| 2026-07-05 | `54dc212` | [Title:VFX 이펙트] |
| 2026-07-05 | `787844d` | [Title:보트 프리팹 |
| 2026-07-05 | `125f097` | [Title: 전신 VFX] |
| 2026-07-05 | `3c04c3d` | [Title:VFX 인스펙터 추가] |
| 2026-07-05 | `209e88d` | 비쥬얼 그래프 패키지 |
| 2026-07-04 | `2ded8cf` | [Title: 게임시작시 검 드로우] |
| 2026-07-04 | `8d84ebe` | [Title, 테스트] |
| 2026-07-04 | `5b1a49a` | [Title 역할별 메서드 분리] |
| 2026-07-04 | `51b9fe9` | [Title:디졸브 머티리얼 프리워밍] |
| 2026-07-04 | `1d47a01` | [Title: 타이틀에서 씬넘어갈때 디졸브 머테리얼로 교체되는 연출 테스트중] |
| 2026-07-03 | `103ea00` | [타이틀씬 여캐 옷벗김] |
| 2026-07-03 | `1fdbe78` | [타이틀 작업중] |
| 2026-07-03 | `d4058f2` | [Allin Shader 에셋] |
| 2026-07-03 | `c39d5d4` | 리팩토링중(별도 namespace) - WeaponBase, TestSwordWeapon추가 |
| 2026-07-03 | `e3fb7c9` | 안쓰는 에셋 정리 |
| 2026-07-03 | `c41300f` | 이펙트 |
| 2026-07-02 | `c464f98` | [전진스킬 고려중 테스트용 코드] |
| 2026-07-02 | `3d5b3e0` | [이펙트 에셋 작업중] |
| 2026-07-02 | `92a32c5` | [Effect:슬래시 이펙트,작업중] |
| 2026-07-02 | `4c0a47b` | [Effect, SO 파일 이름 수정] |
| 2026-07-02 | `8fdbe6f` | [Effect:무기별 이펙트 시스템,테스트중,수정중,Description] |
| 2026-07-02 | `bedbf95` | 네트워크 캐릭터 교체 |
| 2026-07-02 | `9d3bc84` | [Player:이동 방향값을 [Networked]로 노출하고 Render()에서 전 피어가 애니메이터에 반영하도록 변경] |
| 2026-07-02 | `fc34bfb` | [✨ 애니메이션 누락된 메타파일] |
| 2026-07-02 | `2cb715b` | [Animation]:쉴드 애니메이션 |
| 2026-07-01 | `05ba335` | [Player:무브 애니메이션 네트워크 동작] |
| 2026-07-01 | `1950e83` | [안쓰는 에셋 정리] |
| 2026-07-01 | `9a3178d` | Player:플레이어 무브먼트 |
| 2026-07-01 | `0a68eff` | Player:기본공격 구현 |
| 2026-07-01 | `bc42db6` | Player:Bow,ShiledWeapon 리팩토링 |
| 2026-07-01 | `b429cbc` | Player:UltimateState 리팩토링 |
| 2026-07-01 | `b2e5282` | player:SkillState 리팩토링 |
| 2026-07-01 | `28d209a` | Player:AttackingState리팩토링 |
| 2026-07-01 | `ebdd995` | Player:IWeapon 리팩토링 |
| 2026-07-01 | `0919fd9` | Player:WeaponBase 리팩토링 |
| 2026-07-01 | `1b5d3d6` | Player:아바타 에셋 임포트 |
| 2026-06-30 | `7805735` | Player.Combat:튜닝 값을 한 곳(Definition)으로 통일 |
| 2026-06-30 | `8e696e9` | Combat:스킬 구현,쿨타임 적용(임시 스킬,추후변경 가능) |
| 2026-06-30 | `03c0689` | Animator:무기별 애니메이터 오버라이드 추가(빈껍데기) |
| 2026-06-30 | `0779b2a` | Player.Combat:애니메이션 재생은 무기에서 재생되도록 구현 |
| 2026-06-30 | `70e2c60` | Player: 전사 애니메이션 에셋 추가 |
| 2026-06-30 | `2a4efa9` | Player,Combat: 전사 기본 애니메이션 추가 |
| 2026-06-30 | `0c3d47a` | Camera:쿼터뷰 시점 카메라 |
| 2026-06-30 | `0404fe2` | Camera:시네머신 카메라 패키지 다운로드 |
| 2026-06-30 | `4f319f6` | Player:근접 애니메이션 에셋 |
| 2026-06-29 | `5772b64` | Combat:무기 SO 추가 |
| 2026-06-29 | `aea8091` | Combat:무기 SO추가 |
| 2026-06-29 | `64ebcb3` | Combat:모든 무기가 웨폰베이스에 상속되게 수정, 그에따른 cs파일 이름수정 |
| 2026-06-29 | `2fea4ed` | Combat:웨폰베이스 인터페이스 구현 |
| 2026-06-29 | `14b33f9` | Combat:Fix ai가 지워버린 웨폰베이스의 using Game.Combat.Chain; 추가 |
| 2026-06-29 | `b25f854` | Combat:탱커웨폰 추가(코어에 IDamageReducible(방패막기) 인터페이스 추가 필요 그전까지는 주석처리) |
| 2026-06-29 | `bcd4972` | Combat:힐러웨폰 추가(코어에 IHealable 인터페이스 추가필요)그전까지는 힐러웨폰 주석처리 |
| 2026-06-29 | `a076795` | Combat:아처웨폰(원거리무기)추가 및 투사체 추가 |
| 2026-06-29 | `54e8da8` | Combat: 웨폰베이스와 딜러웨폰 추가로 인한 밀리웨폰 리팩토링 |
| 2026-06-29 | `23a29d6` | Combat:딜러웨폰 추가 |
| 2026-06-29 | `8fe3901` | combat:웨폰베이스 |
| 2026-06-29 | `1494a75` | CombatSetup 밀리웨폰에서 웨폰베이스로 변경 |
| 2026-06-26 | `5851cac` | 체인컨트롤러 프리팹 |
| 2026-06-26 | `54eba75` | combat: 풀체인 구현 |
| 2026-06-26 | `880e278` | Dragon 에셋 추가 |
| 2026-06-25 | `856309c` | jcw 테스트 씬 |
| 2026-06-25 | `ee473f3` | combat: MeleeWeapon MVP |
| 2026-06-25 | `3ce6b87` | 무기 데이터(WeaponDefinition)의 so |
| 2026-06-25 | `97ccc68` | combat: 결정론적 판정을 위한 무기 데이터 에셋(WeaponDefinition)이랑 웨폰class 추가 |

</details>
