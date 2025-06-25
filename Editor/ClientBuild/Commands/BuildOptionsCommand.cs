﻿namespace UniGame.UniBuild.Editor.Commands
{
    using System;
    using Editor;
    using Parsers;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Scripting.APIUpdating;

    /// <summary>
    /// https://docs.unity3d.com/ScriptReference/BuildOptions.html
    /// any build parameter and be used by template "-[BuildOptions Item]"
    /// </summary>
    [Serializable]
    [MovedFrom(sourceNamespace:"UniModules.UniGame.UniBuild.Editor.ClientBuild.Commands.PreBuildCommands")]
    public class BuildOptionsCommand : SerializableBuildCommand
    {
        public bool setIncrementalIl2CppBuild = true;
        
        [SerializeField]
        public BuildOptions[] _buildOptions = new BuildOptions[] {BuildOptions.None};

        public override void Execute(IUniBuilderConfiguration configuration)
        {
            var enumBuildOptionsParser = new EnumArgumentParser<BuildOptions>();
            var buildOptions = enumBuildOptionsParser.Parse(configuration.Arguments);
            var options = BuildOptions.None;
            
            for (int i = 0; i < buildOptions.Count; i++) {
                options |= buildOptions[i];
            }

            foreach (var buildValue in _buildOptions) {
                options |= buildValue;
            }
            
            configuration.BuildParameters.SetBuildOptions(options,false);
            
        }
        
    }
}
