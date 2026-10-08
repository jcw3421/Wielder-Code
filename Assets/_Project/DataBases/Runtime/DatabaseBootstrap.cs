using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.DataBases
{
    public class DatabaseBootstrap : MonoBehaviour
    {
        private const float FakeProgressMinSpeed = 0.15f;
        private const float FakeProgressMaxSpeed = 1.2f;
        private const float CompletionFillSpeed = 1.25f;

        private static DatabaseBootstrap _instance;

        private enum DataSource { ScriptableObject, GoogleSheet }

        [SerializeField] private DataSource _dataSource;

        [Header("SO Mode")]
        [SerializeField] private SOItemDB _itemDB;
        [SerializeField] private SODropTableDB _dropTableDB;
        [SerializeField] private SOInventoryItemDB _inventoryItemDB;
        [SerializeField] private SODropItemDB _dropItemDB;
        [SerializeField] private SOWeaponDB _weaponDB;
        [SerializeField] private SOBossDB _bossDB;

        [Header("Google Sheet Mode")]
        [SerializeField] private GoogleSheetDB _googleSheetDB;

        [Header("Loading UI")]
        [SerializeField] private GameObject _loadingCanvas;
        [SerializeField] private Image _loadingIcon;
        [SerializeField] private Slider _progressBar;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private float _spinSpeed = 360f;

        [Header("On Complete")]
        [SerializeField] private UnityEvent _onLoadComplete;

        private bool _isLoading;
        private float _targetProgress;

        private async void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            _isLoading = true;
            if (_progressBar != null) _progressBar.value = 0f;
            SetProgress(0f, "게임 데이터를 준비하고 있습니다...");
            await Awaitable.NextFrameAsync();

            switch (_dataSource)
            {
                case DataSource.ScriptableObject:
                    await InitializeSOMode();
                    break;
                case DataSource.GoogleSheet:
                    await InitializeGoogleSheetMode();
                    break;
            }

            SetProgress(1f, "준비가 완료되었습니다.");
            await WaitForProgressBarAsync();
            _isLoading = false;

            if (_loadingCanvas != null)
                _loadingCanvas.SetActive(false);

            _onLoadComplete?.Invoke();
        }

        private void Update()
        {
            if (!_isLoading) return;

            if (_loadingIcon != null)
                _loadingIcon.transform.Rotate(0f, 0f, -_spinSpeed * Time.unscaledDeltaTime);

            if (_progressBar != null)
            {
                if (_targetProgress < 1f)
                {
                    var progressRatio = _targetProgress > 0f
                        ? _progressBar.value / _targetProgress
                        : 0f;
                    var fillSpeed = Mathf.Lerp(
                        FakeProgressMinSpeed,
                        FakeProgressMaxSpeed,
                        progressRatio);
                    _progressBar.value = Mathf.MoveTowards(
                        _progressBar.value,
                        _targetProgress,
                        fillSpeed * Time.unscaledDeltaTime);
                }
                else
                {
                    _progressBar.value = Mathf.MoveTowards(
                        _progressBar.value,
                        1f,
                        CompletionFillSpeed * Time.unscaledDeltaTime);
                }
            }
        }

        private async Awaitable InitializeSOMode()
        {
            const int totalSteps = 6;

            SetProgress(0f / totalSteps, "아이템 정보를 불러오고 있습니다...");
            await _itemDB.Initialize();

            SetProgress(1f / totalSteps, "보상 정보를 불러오고 있습니다...");
            await _dropTableDB.Initialize();

            SetProgress(2f / totalSteps, "인벤토리 정보를 불러오고 있습니다...");
            await _inventoryItemDB.Initialize();

            SetProgress(3f / totalSteps, "전리품 정보를 불러오고 있습니다...");
            await _dropItemDB.Initialize();

            SetProgress(4f / totalSteps, "무기 정보를 불러오고 있습니다...");
            await _weaponDB.Initialize();

            SetProgress(5f / totalSteps, "보스 정보를 불러오고 있습니다...");
            await _bossDB.Initialize();

            ServiceLocator.Register<IItemDB>(_itemDB);
            ServiceLocator.Register<IDropTableDB>(_dropTableDB);
            ServiceLocator.Register<IInventoryItemDB>(_inventoryItemDB);
            ServiceLocator.Register<IDropItemDB>(_dropItemDB);
            ServiceLocator.Register<IWeaponDB>(_weaponDB);
            ServiceLocator.Register<IBossDB>(_bossDB);
        }

        private async Awaitable InitializeGoogleSheetMode()
        {
            SetProgress(0.9f, "최신 게임 데이터를 동기화하고 있습니다...");
            await _googleSheetDB.Initialize();

            ServiceLocator.Register<IItemDB>(_googleSheetDB);
            ServiceLocator.Register<IDropTableDB>(_googleSheetDB);
            ServiceLocator.Register<IInventoryItemDB>(_googleSheetDB);
            ServiceLocator.Register<IDropItemDB>(_googleSheetDB);
            ServiceLocator.Register<IWeaponDB>(_googleSheetDB);
            ServiceLocator.Register<IBossDB>(_googleSheetDB);
            ServiceLocator.Register<IRoomCRUD>(_googleSheetDB);
        }

        private void SetProgress(float normalized, string status)
        {
            _targetProgress = Mathf.Clamp01(normalized);
            if (_statusText != null) _statusText.text = status;
        }

        private async Awaitable WaitForProgressBarAsync()
        {
            while (_progressBar != null && _progressBar.value < 0.999f)
                await Awaitable.NextFrameAsync();

            if (_progressBar != null) _progressBar.value = 1f;
        }

        private void OnDestroy()
        {
            // 복제본(씬 재진입 시 생기는 사본)은 Awake에서 Destroy된다. 그 OnDestroy가 원본 등록을
            // 지우면 IRoomCRUD 등 DB 서비스가 통째로 사라진다. 원본(_instance == this)만 해제한다.
            if (_instance != this) return;
            _instance = null;

            ServiceLocator.Unregister<IItemDB>();
            ServiceLocator.Unregister<IDropTableDB>();
            ServiceLocator.Unregister<IInventoryItemDB>();
            ServiceLocator.Unregister<IDropItemDB>();
            ServiceLocator.Unregister<IWeaponDB>();
            ServiceLocator.Unregister<IBossDB>();
            ServiceLocator.Unregister<IRoomCRUD>();
        }
    }
}
