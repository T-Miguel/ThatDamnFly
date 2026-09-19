// Reproducible project configuration (batchmode): TMP essentials, brand PNGs as sprites, main scene,
// PlayerSettings per target and builds. Usage: Unity -batchmode -executeMethod ThatDamnFly.Editor.ProjectSetup.Configure | BuildWeb | BuildAndroidApk
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ThatDamnFly.Editor
{
    public static class ProjectSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string Version = "1.2.4";

        [MenuItem("That Damn Fly/Configure Project")]
        public static void Configure()
        {
            ImportTmpEssentials();
            CreateFontAssets();
            ConfigurePlugins();
            MarkSprites();
            EnsureScene();
            ApplyPlayerSettings();
            ApplyIconsAndSplash();
            AssetDatabase.SaveAssets();
            Debug.Log("[TDF] Configure done");
        }

        /// <summary>App icons from the Brand Kit (Assets/Brand) and splash screen with the logo (the Unity logo is mandatory on the Personal license).</summary>
        static void ApplyIconsAndSplash()
        {
            Texture2D Tex(string name)
            {
                string path = "Assets/Brand/" + name; var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp != null && (imp.textureType != TextureImporterType.Default || imp.mipmapEnabled || imp.npotScale != TextureImporterNPOTScale.None || imp.textureCompression != TextureImporterCompression.Uncompressed))
                { imp.textureType = TextureImporterType.Default; imp.mipmapEnabled = false; imp.npotScale = TextureImporterNPOTScale.None; imp.textureCompression = TextureImporterCompression.Uncompressed; imp.alphaIsTransparency = true; imp.SaveAndReimport(); }
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            var master = Tex("app-icon-master-v1.png"); var fg = Tex("android-adaptive-foreground-v1.png"); var bg = Tex("android-adaptive-background-v1.png"); var mono = Tex("android-adaptive-monochrome-v1.png");
            if (master == null) { Debug.LogWarning("[TDF] Assets/Brand/app-icon-master-v1.png missing"); return; }
            foreach (var target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS })
            {
                foreach (var kind in PlayerSettings.GetSupportedIconKinds(target))
                {
                    var icons = PlayerSettings.GetPlatformIcons(target, kind);
                    foreach (var icon in icons)
                    {
                        if (icon.maxLayerCount >= 2 && fg != null && bg != null) icon.SetTextures(bg, fg);   // Android adaptive: background + foreground (the monochrome goes in the manifest when there is one)
                        else icon.SetTexture(master);
                    }
                    PlayerSettings.SetPlatformIcons(target, kind, icons);
                }
            }
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { master }, IconKind.Any);
            // splash screen: paper background, our logo after Unity's
            var logoPath = "Assets/Brand/logo-stacked-color-v1.png"; var limp = AssetImporter.GetAtPath(logoPath) as TextureImporter;
            if (limp != null && limp.textureType != TextureImporterType.Sprite) { limp.textureType = TextureImporterType.Sprite; limp.mipmapEnabled = false; limp.alphaIsTransparency = true; limp.SaveAndReimport(); }
            var logo = AssetDatabase.LoadAssetAtPath<Sprite>(logoPath);
            PlayerSettings.SplashScreen.show = true; PlayerSettings.SplashScreen.backgroundColor = new Color(0.969f, 0.941f, 0.871f); PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.LightOnDark;
            PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.AllSequential; PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Dolly; PlayerSettings.SplashScreen.overlayOpacity = 0f;
            if (logo != null) PlayerSettings.SplashScreen.logos = new[] { PlayerSettings.SplashScreenLogo.Create(2f, logo) };
            Debug.Log("[TDF] icons and splash screen applied");
        }

        static void ImportTmpEssentials()
        {
            // The TMP essentials are extracted by tools/extract-unitypackage.py (ImportPackage is asynchronous in batchmode).
            if (!Directory.Exists("Assets/TextMesh Pro/Resources")) Debug.LogError("[TDF] Assets/TextMesh Pro/Resources missing: run tools/extract-unitypackage.py");
        }

        /// <summary>Creates the Archivo TMP_FontAssets (400/600/900) with a pre-filled atlas (PT/EN, accents, symbols) in Resources/fonts.</summary>
        static void CreateFontAssets()
        {
            const string chars = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~ÀÁÂÃÄÇÈÉÊËÌÍÎÏÒÓÔÕÖÙÚÛÜàáâãäçèéêëìíîïòóôõöùúûüÑñ€–-…«»‘’“”·×≤≥°";
            foreach (var w in new[] { "Regular", "SemiBold", "Black" })
            {
                string fontPath = "Assets/Fonts/Archivo/Archivo-" + w + ".ttf"; string assetPath = "Assets/Resources/fonts/Archivo-" + w + " SDF.asset";
                if (File.Exists(assetPath)) continue;
                var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath); if (font == null) { Debug.LogWarning("[TDF] font missing: " + fontPath); continue; }
                var fa = TMPro.TMP_FontAsset.CreateFontAsset(font, 72, 8, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, TMPro.AtlasPopulationMode.Dynamic, true);
                fa.name = "Archivo-" + w + " SDF";
                Directory.CreateDirectory("Assets/Resources/fonts");
                AssetDatabase.CreateAsset(fa, assetPath);
                fa.material.name = fa.name + " Material"; AssetDatabase.AddObjectToAsset(fa.material, fa);
                fa.atlasTextures[0].name = fa.name + " Atlas"; AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
                fa.TryAddCharacters(chars, out string missing);
                if (!string.IsNullOrEmpty(missing)) Debug.LogWarning("[TDF] glyphs missing in " + w + ": " + missing);
                EditorUtility.SetDirty(fa); AssetDatabase.SaveAssets();
                Debug.Log("[TDF] font asset created: " + assetPath);
            }
        }

        /// <summary>Platforms of the FlyCore native plugins (the automatic import did not fill the PluginImporter).</summary>
        static void ConfigurePlugins()
        {
            SetPlugin("Assets/Plugins/WebGL/flycore_amalgam.cpp", imp => { imp.SetCompatibleWithPlatform(BuildTarget.WebGL, true); });
            SetPlugin("Assets/Plugins/iOS/flycore_amalgam.cpp", imp => { imp.SetCompatibleWithPlatform(BuildTarget.iOS, true); });
            SetPlugin("Assets/Plugins/iOS/TDFShare.mm", imp => { imp.SetCompatibleWithPlatform(BuildTarget.iOS, true); });
            SetPlugin("Assets/Plugins/WebGL/tdf.jslib", imp => { imp.SetCompatibleWithPlatform(BuildTarget.WebGL, true); });
            SetPlugin("Assets/Plugins/Android/arm64-v8a/libflycore.so", imp => { imp.SetCompatibleWithPlatform(BuildTarget.Android, true); imp.SetPlatformData(BuildTarget.Android, "CPU", "ARM64"); });
            SetPlugin("Assets/Plugins/x86_64/flycore.dll", imp => { imp.SetCompatibleWithEditor(true); imp.SetEditorData("CPU", "x86_64"); imp.SetEditorData("OS", "Windows"); imp.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, true); imp.SetPlatformData(BuildTarget.StandaloneWindows64, "CPU", "x86_64"); });
        }

        static void SetPlugin(string path, System.Action<PluginImporter> apply)
        {
            var imp = AssetImporter.GetAtPath(path) as PluginImporter;
            if (imp == null) { Debug.LogError("[TDF] plugin not recognized: " + path); return; }
            imp.SetCompatibleWithAnyPlatform(false);
            foreach (BuildTarget t in new[] { BuildTarget.WebGL, BuildTarget.iOS, BuildTarget.Android, BuildTarget.StandaloneWindows64, BuildTarget.StandaloneWindows, BuildTarget.StandaloneOSX, BuildTarget.StandaloneLinux64 }) imp.SetCompatibleWithPlatform(t, false);
            imp.SetCompatibleWithEditor(false);
            apply(imp); imp.SaveAndReimport();
            Debug.Log("[TDF] plugin configured: " + path);
        }

        static void MarkSprites()
        {
            var paths = new System.Collections.Generic.List<string>();
            foreach (var dir in new[] { "Assets/Resources/brand", "Assets/Resources/art" }) if (Directory.Exists(dir)) foreach (var f in Directory.GetFiles(dir, "*.png")) paths.Add(f.Replace(System.IO.Path.DirectorySeparatorChar, '/'));
            foreach (var path in paths)
            {
                var imp = AssetImporter.GetAtPath(path) as TextureImporter; if (imp == null) { Debug.LogWarning("[TDF] missing " + path); continue; }
                bool changed = false;
                if (imp.textureType != TextureImporterType.Sprite) { imp.textureType = TextureImporterType.Sprite; changed = true; }
                if (imp.spriteImportMode != SpriteImportMode.Single) { imp.spriteImportMode = SpriteImportMode.Single; changed = true; }
                if (!imp.alphaIsTransparency) { imp.alphaIsTransparency = true; changed = true; }
                if (imp.mipmapEnabled) { imp.mipmapEnabled = false; changed = true; }
                int maxSize = path.Contains("kitchen_bg") ? 2048 : 1024;
                if (imp.maxTextureSize != maxSize) { imp.maxTextureSize = maxSize; changed = true; }
                if (imp.spritePixelsPerUnit != 100) { imp.spritePixelsPerUnit = 100; changed = true; }
                if (changed) imp.SaveAndReimport();
            }
        }

        static void EnsureScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("Bootstrap"); go.AddComponent<Presentation.Bootstrap>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "That Damn Fly"; PlayerSettings.productName = "That Damn Fly"; PlayerSettings.bundleVersion = Version;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true; PlayerSettings.allowedAutorotateToPortraitUpsideDown = false; PlayerSettings.allowedAutorotateToLandscapeLeft = false; PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.thatdamnfly.game");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.thatdamnfly.game");
            PlayerSettings.runInBackground = false;
            // Web
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.High);
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.dataCaching = false;
            PlayerSettings.WebGL.nameFilesAsHashes = true; // immutable paths: every build has new names; index.html without cache
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None; // dev builds: ExplicitlyThrownExceptionsOnly (see BuildWeb)
            PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
            PlayerSettings.WebGL.wasm2023 = false; // Safari iOS 15: do not assume recent wasm features
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL, Il2CppCompilerConfiguration.Release);
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.WebGL.template = "PROJECT:TDF";
            PlayerSettings.WebGL.threadsSupport = false;
            PlayerSettings.WebGL.initialMemorySize = 64;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
            // Android
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)29;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.High);
            PlayerSettings.Android.bundleVersionCode = 1;
            // iOS
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.buildNumber = "1";
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { UnityEngine.Rendering.GraphicsDeviceType.Metal });
        }

        static string Arg(string name, string def)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return def;
        }

        [MenuItem("That Damn Fly/Build Web (dev)")]
        public static void BuildWeb()
        {
            Configure();
            bool dev = Arg("-tdfDev", "1") == "1";
            string outDir = Arg("-tdfOut", Path.GetFullPath("../dist/web-dev"));
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.WebGL, dev ? "TDF_DEVBUILD" : "");
            PlayerSettings.WebGL.exceptionSupport = dev ? WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly : WebGLExceptionSupport.None;
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL, dev ? Il2CppCompilerConfiguration.Release : Il2CppCompilerConfiguration.Master);
            var opts = new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = outDir, target = BuildTarget.WebGL, options = BuildOptions.None };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[TDF] Web build {report.summary.result} -> {outDir} | {report.summary.totalSize} bytes | {report.summary.totalTime}");
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        [MenuItem("That Damn Fly/Build Android APK (dev)")]
        public static void BuildAndroidApk()
        {
            Configure();
            bool dev = Arg("-tdfDev", "1") == "1";
            string outPath = Arg("-tdfOut", Path.GetFullPath("../dist/android/ThatDamnFly-dev.apk"));
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, dev ? "TDF_DEVBUILD" : "");
            EditorUserBuildSettings.buildAppBundle = false;
            var opts = new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = outPath, target = BuildTarget.Android, options = BuildOptions.None };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[TDF] Android build {report.summary.result} -> {outPath} | {report.summary.totalSize} bytes | {report.summary.totalTime}");
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
