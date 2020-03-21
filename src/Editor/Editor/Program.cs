using Engine.Behaviors;
using Engine.Graphics;
using System.Runtime.InteropServices;
using Veldrid.Platform;
using Veldrid.Graphics;
using System.IO;
using System;
using Engine.Physics;
using Engine.Assets;
using System.Reflection;
using Engine.ProjectSystem;
using Engine.Audio;

namespace Engine.Editor
{
    public class Program
    {
        public static Game Game { get; } = new Game();

        public static void Main(string[] args)
        {
            CommandLineOptions commandLineOptions = new CommandLineOptions(args);
            // Force-load prefs.
            var prefs = EditorPreferences.Instance;

            OpenTKWindow window = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? (OpenTKWindow)new DedicatedThreadWindow(960, 540, WindowState.Maximized)
                : new SameThreadWindow(960, 540, WindowState.Maximized);
            window.Title = "ge.Editor";
            GraphicsBackEndPreference backEndPref = commandLineOptions.PreferOpenGL ? GraphicsBackEndPreference.OpenGL : GraphicsBackEndPreference.None;
            GraphicsSystem gs = new GraphicsSystem(window, prefs.RenderQuality, backEndPref);
            gs.Context.ResourceFactory.AddShaderLoader(new EmbeddedResourceShaderLoader(typeof(Program).GetTypeInfo().Assembly));
            Game.SystemRegistry.Register(gs);
            Game.LimitFrameRate = prefs.LimitFramerate;

            InputSystem inputSystem = new InputSystem(window);
            inputSystem.RegisterCallback((input) =>
            {
                if (input.GetKeyDown(Key.F4) && (input.GetKey(Key.AltLeft) || input.GetKey(Key.AltRight)))
                {
                    Game.Exit();
                }
            });

            Game.SystemRegistry.Register(inputSystem);

            ImGuiRenderer imGuiRenderer = new ImGuiRenderer(gs.Context, window.NativeWindow, inputSystem);
            gs.SetImGuiRenderer(imGuiRenderer);

            var als = new AssemblyLoadSystem();
            Game.SystemRegistry.Register(als);

            AssetSystem assetSystem = new EditorAssetSystem(Path.Combine(AppContext.BaseDirectory, "Assets"), als.Binder);
            Game.SystemRegistry.Register(assetSystem);

            EditorSceneLoaderSystem esls = new EditorSceneLoaderSystem(Game, Game.SystemRegistry.GetSystem<GameObjectQuerySystem>());
            Game.SystemRegistry.Register<SceneLoaderSystem>(esls);
            esls.AfterSceneLoaded += () => Game.ResetDeltaTime();

            CommandLineOptions.AudioEnginePreference? audioPreference = commandLineOptions.AudioPreference;
            AudioEngineOptions audioEngineOptions =
                !audioPreference.HasValue ? AudioEngineOptions.Default
                : audioPreference == CommandLineOptions.AudioEnginePreference.None ? AudioEngineOptions.UseNullAudio
                : AudioEngineOptions.UseOpenAL;
            AudioSystem audioSystem = new AudioSystem(audioEngineOptions);
            Game.SystemRegistry.Register(audioSystem);

            BehaviorUpdateSystem bus = new BehaviorUpdateSystem(Game.SystemRegistry);
            Game.SystemRegistry.Register(bus);
            bus.Register(imGuiRenderer);

            PhysicsSystem ps = new PhysicsSystem(PhysicsLayersDescription.Default);
            Game.SystemRegistry.Register(ps);

            ConsoleCommandSystem ccs = new ConsoleCommandSystem(Game.SystemRegistry);
            Game.SystemRegistry.Register(ccs);

            Game.SystemRegistry.Register(new SynchronizationHelperSystem());

            window.Closed += Game.Exit;

            var editorSystem = new EditorSystem(Game.SystemRegistry, commandLineOptions, imGuiRenderer);
            editorSystem.DiscoverComponentsFromAssembly(typeof(Program).GetTypeInfo().Assembly);
            // Editor system registers itself.

            Game.RunMainLoop();

            window.NativeWindow.Dispose();

            EditorPreferences.Instance.Save();
        }
    }
}
