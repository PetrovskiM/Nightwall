using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Nightwall.Editor
{
    /// <summary>
    /// Menu items that switch the active build target to Android or iOS and configure
    /// mobile-friendly Player settings (landscape, fullscreen, no status bar).
    /// </summary>
    public static class MobileBuildTarget
    {
        [MenuItem("Nightwall/Set Build Target/Android")]
        static void SwitchToAndroid()
        {
            ApplyMobilePlayerSettings();
            PlayerSettings.SetApplicationIdentifier(
                NamedBuildTarget.Android, "com.nightwall.game");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.Android, BuildTarget.Android);
            Debug.Log("[Nightwall] Switched build target → Android");
        }

        [MenuItem("Nightwall/Set Build Target/iOS")]
        static void SwitchToiOS()
        {
            ApplyMobilePlayerSettings();
            PlayerSettings.SetApplicationIdentifier(
                NamedBuildTarget.iOS, "com.nightwall.game");
            PlayerSettings.iOS.targetOSVersionString = "13.0";
            EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.iOS, BuildTarget.iOS);
            Debug.Log("[Nightwall] Switched build target → iOS");
        }

        static void ApplyMobilePlayerSettings()
        {
            // Landscape-only, no auto-rotation.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        }
    }
}
