using System;
using PokeBar.Models;
using PokeBar.Services;
using PokeBar.ViewModels;
using PokeBar.Views;

namespace PokeBar
{
    public partial class App : System.Windows.Application
    {
        private TrayService? _tray;
        private StateService? _stateService;
        private TaskbarService? _taskbarService;
        private SpriteService? _spriteService;
        private DexService? _dexService;
        private BattleService? _battleService;
        private GameState? _state;
        private SpriteWindow? _window;
        private WildSpriteWindow? _wildWindow;
        private CaptureOverlayWindow? _captureWindow;
        private PokeballWindow? _pokeballWindow;

        protected override void OnStartup(System.Windows.StartupEventArgs e)
        {
            base.OnStartup(e);

            MainViewModel? vm = null;
            try
            {
                _stateService = new StateService();
                _state = _stateService.Load();

                _taskbarService = new TaskbarService();
                _spriteService = new SpriteService();
                _dexService = new DexService(_spriteService);
                _battleService = new BattleService(_state, _dexService);

                vm = new MainViewModel(_state, _stateService, _spriteService, _taskbarService, _battleService);

                _window = new SpriteWindow { DataContext = vm };
                _window.Show();

                _wildWindow = new WildSpriteWindow { DataContext = vm };
                _wildWindow.Show();
                _wildWindow.Visibility = System.Windows.Visibility.Hidden;
                
                // Definir referência para acesso da Pokébola
                vm.WildWindow = _wildWindow;

                _pokeballWindow = new PokeballWindow { DataContext = vm };
                _pokeballWindow.Show();
                _pokeballWindow.Visibility = System.Windows.Visibility.Collapsed;

                _captureWindow = new CaptureOverlayWindow();
                _captureWindow.Attach(vm, _window, _wildWindow);
                _captureWindow.Show();
                _captureWindow.DisableCaptureMode();

                _tray = new TrayService(vm);
                _tray.Initialize();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Erro ao iniciar: {ex.Message}\n\nStack: {ex.StackTrace}", "PokeBar - Erro", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                Shutdown();
                return;
            }

            if (vm == null) return;

            vm.ManualCaptureModeChanged += (_, active) => Current?.Dispatcher?.Invoke(() =>
            {
                if (active)
                {
                    _captureWindow?.EnableCaptureMode();
                }
                else
                {
                    _captureWindow?.DisableCaptureMode();
                }
            });

            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(vm.WildVisible))
                {
                    Current?.Dispatcher?.Invoke(() =>
                    {
                        if (_wildWindow != null)
                        {
                            _wildWindow.Visibility = vm.WildVisible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden;
                        }
                    });
                }
            };

            vm.Initialize();
        }

        protected override void OnExit(System.Windows.ExitEventArgs e)
        {
            try
            {
                _stateService?.Save();
            }
            catch { }
            _tray?.Dispose();
            _wildWindow?.Close();
            _captureWindow?.Close();
            base.OnExit(e);
        }
    }
}
