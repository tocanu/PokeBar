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
        private BattleService? _battleService;
        private GameState? _state;
        private SpriteWindow? _window;

        protected override void OnStartup(System.Windows.StartupEventArgs e)
        {
            base.OnStartup(e);

            _stateService = new StateService();
            _state = _stateService.Load();

            _taskbarService = new TaskbarService();
            _spriteService = new SpriteService();
            _battleService = new BattleService(_state);

            var vm = new MainViewModel(_state, _stateService, _spriteService, _taskbarService, _battleService);

            _window = new SpriteWindow { DataContext = vm };
            _window.Show();

            _tray = new TrayService(vm);
            _tray.Initialize();

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
            base.OnExit(e);
        }
    }
}
