using System.Windows;
using System.Windows.Input;
using MapUiApp.Engine;

namespace MapUiApp
{
    public partial class MainWindow : Window
    {
        private readonly MapScene _scene;

        public MainWindow(string mapId)
        {
            InitializeComponent();
            _scene = new MapScene(mapId);
            Host.Children.Add(_scene);
            KeyDown += OnKeyDown;
            UpdateTitle();
        }

        private void UpdateTitle()
        {
            Title = $"JX3 Map UI — {_scene.MapDisplay} · {_scene.SituationLabel} · M 地图 · B 战场 · Tab 切换 · 1-7 地图";
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab)
            {
                _scene.ToggleSituation();
                UpdateTitle();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.M)
            {
                _scene.MiddleOpen = !_scene.MiddleOpen;
                e.Handled = true;
            }
            else if (e.Key == Key.B)
            {
                _scene.BattleVisible = !_scene.BattleVisible;
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                _scene.MiddleOpen = false;
                e.Handled = true;
            }
            else if (e.Key >= Key.D1 && e.Key <= Key.D7)
            {
                _scene.SelectMap(GameData.Maps[(int)e.Key - (int)Key.D1].Id);
                UpdateTitle();
                e.Handled = true;
            }
        }
    }
}
