namespace UniGame.UniBuild.Editor.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Editor;
    using global::Editor.Tools;
    using Inspector;
    using UnityEditor;
    using UnityEditor.Build.Profile;
    using UnityEngine;
    using UnityEngine.Scripting.APIUpdating;

#if ODIN_INSPECTOR
    using Sirenix.OdinInspector;
#endif

#if TRI_INSPECTOR
    using TriInspector;
#endif

    [Serializable]
    [MovedFrom(sourceNamespace:"UniModules.UniGame.UniBuild.Editor.ClientBuild.Commands.PreBuildCommands")]
    [BuildCommandMetadata(
        displayName: "Apply Scripting Define Symbols",
        description: "Manages conditional compilation symbols for different build targets and configurations, enabling platform-specific code compilation.",
        category: "Build Configuration"
    )]
    public class ApplyScriptingDefineSymbolsCommand : SerializableBuildCommand
    {
        private const string DefinesSeparator = ";";

        [SerializeField]
        public string definesKey = "-defineValues";

#if ODIN_INSPECTOR
        [Searchable]
#endif
        [SerializeField]
        public List<string> defaultDefines = new List<string>();

#if ODIN_INSPECTOR
        [Searchable]
#endif
        [SerializeField]
        public List<string> removeDefines = new List<string>();

        public override void Execute(IUniBuilderConfiguration configuration)
        {
            if (!configuration.Arguments.GetStringValue(definesKey, out var defineValues))
            {
                defineValues = string.Empty;
            }

            var profile = configuration.BuildParameters?.buildProfile;
            if (profile == null)
            {
                Execute(defineValues);
                return;
            }

            ApplyToProfile(profile, defineValues);
        }

        public void Execute(string defineValues)
        {
            EditorSettingsUtility.ApplyDefines(defaultDefines, removeDefines, defineValues);
        }

        private void ApplyToProfile(BuildProfile profile, string defineValues)
        {
            var defines = profile.scriptingDefines
                .Concat(defineValues.Split(new[] { DefinesSeparator },
                    StringSplitOptions.RemoveEmptyEntries))
                .Concat(defaultDefines)
                .Where(value => !string.IsNullOrWhiteSpace(value) && !removeDefines.Contains(value))
                .Distinct()
                .ToArray();

            if (profile.scriptingDefines.SequenceEqual(defines))
                return;

            profile.scriptingDefines = defines;
            AssetDatabase.SaveAssetIfDirty(profile);
        }

#if ODIN_INSPECTOR || TRI_INSPECTOR
        [Button]
#endif
        public void Execute() => Execute(string.Empty);
    }
}
