using System.Windows;
using MapUiApp.Engine;

namespace MapUiApp
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            GameData.Locate();

            string dumpDirectory = null;
            string probeDirectory = null;
            var mapId = GameData.Maps[0].Id;
            for (int i = 0; i < e.Args.Length; i++)
            {
                if (e.Args[i] == "--dump" && i + 1 < e.Args.Length) dumpDirectory = e.Args[i + 1];
                else if (e.Args[i] == "--probe" && i + 1 < e.Args.Length) probeDirectory = e.Args[i + 1];
                else if (e.Args[i] == "--map" && i + 1 < e.Args.Length) mapId = e.Args[i + 1];
            }

            if (probeDirectory != null)
            {
                Probe.Run(probeDirectory);
                Shutdown();
                return;
            }

            if (dumpDirectory != null)
            {
                var scene = new MapScene(mapId);
                scene.Dump(dumpDirectory);
                Shutdown();
                return;
            }

            new MainWindow(mapId).Show();
        }
    }
}
