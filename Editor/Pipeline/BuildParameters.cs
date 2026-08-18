namespace UniGame.UniBuild.Editor
{
    using System;
    using System.Collections.Generic;
    using Utils;
    using ClientBuild;
    using ClientBuild.BuildConfiguration;
    using UnityEditor;
    using UnityEditor.Build;
    using UnityEditor.Build.Profile;
    using UnityEngine;

#if UNITY_WEBGL
    using UnityEditor.WebGL;
#endif

    [Serializable]
    public class BuildParameters {

        public const string BuildFolder = "Build";
        
        public bool printBuildReport = true;
        
        public BuildProfile buildProfile;
        public BuildTarget buildTarget;
        public BuildTargetGroup buildTargetGroup;
        /// <summary>Standalone subtarget for this build without changing the active Editor subtarget.</summary>
        public StandaloneBuildSubtarget standaloneBuildSubtarget;
        public ScriptingImplementation scriptingImplementation = ScriptingImplementation.Mono2x;

        public int                            buildNumber  = 0;
        public string                         productName = string.Empty;
        public string                         outputFolder = "Builds";
        public string                         outputFile   = "artifact";
        public string                         artifactPath;
        public BuildOptions                   buildOptions = BuildOptions.None;
        public List<EditorBuildSettingsScene> scenes       = new List<EditorBuildSettingsScene>();

        public string projectId = string.Empty;
        public string bundleId = string.Empty;
        public string companyName = string.Empty;
        public string bundleVersion = string.Empty;
            
        //Android
        public string keyStorePath;
        public string keyStorePass;
        public string keyStoreAlias;
        public string keyStoreAliasPass;
        public string branch = string.Empty;
        
        //webgl
        public WebGlBuildData webGlBuildData = new WebGlBuildData();
        public BuildEnvironmentType environmentType = BuildEnvironmentType.Custom;

        private UniBuildConfigurationData _buildData;
        private IArgumentsProvider _arguments;
        
        public BuildParameters(UniBuildConfigurationData buildData, IArgumentsProvider arguments)
        {
            _buildData = buildData;
            _arguments = arguments;
            standaloneBuildSubtarget = buildData.standaloneBuildSubTarget;
        }

        public void Execute()
        {
            buildProfile = _buildData.useBuildProfile ? _buildData.buildProfile : null;

            if (buildProfile != null)
            {
                BuildProfile.SetActiveBuildProfile(buildProfile);
            }
            
            var buildArguments = _buildData.buildArguments;
            if (buildArguments.isEnable)
            {
                var args = _buildData.buildArguments.arguments;
                foreach (var argument in args)
                {
                    if(_arguments.Contains(argument.Key)) continue;
                    _arguments.SetArgument(argument.Key,argument.Value.Value);
                }
            }

            productName = PlayerSettings.productName;
            SetProductName(productName,_buildData,_arguments);
            
            outputFile = PlayerSettings.productName;
            if(_buildData.overrideArtifactName && !string.IsNullOrEmpty(_buildData.artifactName))
                outputFile = _buildData.artifactName;
            
            outputFile = outputFile.Replace(" ", "_");

            bundleId = PlayerSettings.applicationIdentifier;
            if(_buildData.overrideBundleName && !string.IsNullOrEmpty(_buildData.bundleName))
                bundleId = _buildData.bundleName;
            
            companyName = PlayerSettings.companyName;
            if(_buildData.overrideCompanyName && !string.IsNullOrEmpty(_buildData.companyName))
                companyName = _buildData.companyName;
            
            printBuildReport=  _buildData.printBuildReport;
            buildTarget      = _buildData.buildTarget;
            buildTargetGroup = _buildData.buildTargetGroup;
            standaloneBuildSubtarget = _buildData.standaloneBuildSubTarget;
            scriptingImplementation = _buildData.scriptingImplementation;
            
            UpdateBuildOptions(_buildData,_arguments);
            UpdateLoggingSettings(_buildData,_arguments);
            UpdateArguments(_arguments);
            
            if (buildArguments.isEnable)
            {
                foreach (var argument in _buildData.buildArguments.arguments)
                {
                    if (argument.Value.Override)
                        _arguments.SetArgument(argument.Key, argument.Value.Value);
                }
            }
            
            var namedTarget = standaloneBuildSubtarget is 
                StandaloneBuildSubtarget.Player or
#if UNITY_2023_1_OR_NEWER
                StandaloneBuildSubtarget.Default
#else
                StandaloneBuildSubtarget.NoSubtarget
#endif
                ? NamedBuildTarget.Standalone
                : NamedBuildTarget.Server;

            PlayerSettings.SetScriptingBackend(namedTarget, scriptingImplementation);
            PlayerSettings.SetIl2CppCodeGeneration(namedTarget,_buildData.il2CppCodeGeneration);
            PlayerSettings.SetIl2CppCompilerConfiguration(namedTarget,_buildData.cppCompilerConfiguration);
            PlayerSettings.bundleVersion = bundleVersion;
            PlayerSettings.applicationIdentifier = bundleId;
            PlayerSettings.SetManagedStrippingLevel(namedTarget,_buildData.strippingLevel);
            
            UpdateWebGLData(_buildData.webGlBuildData);
            UpdateWebGLData(_arguments);
            
            EditorUserBuildSettings.development = buildOptions.IsFlagSet(BuildOptions.Development);
            EditorUserBuildSettings.connectProfiler = buildOptions.IsFlagSet(BuildOptions.ConnectWithProfiler);
            EditorUserBuildSettings.buildWithDeepProfilingSupport =  buildOptions.IsFlagSet(BuildOptions.EnableDeepProfilingSupport);
            EditorUserBuildSettings.allowDebugging = buildOptions.IsFlagSet(BuildOptions.AllowDebugging);
            
            var file   = outputFile;
            var folder = outputFolder;
            var resultArtifactPath = folder.CombinePath(file);
            artifactPath = resultArtifactPath;
        }

        public void UpdateLoggingSettings(UniBuildConfigurationData buildData, IArgumentsProvider arguments)
        {
            if (!buildData.overrideLogsSettings) return;
            PlayerSettings.SetStackTraceLogType(LogType.Log, buildData.logsLevel);
            PlayerSettings.SetStackTraceLogType(LogType.Warning, buildData.warningLevel);
            PlayerSettings.SetStackTraceLogType(LogType.Error, buildData.errorLevel);
            PlayerSettings.SetStackTraceLogType(LogType.Exception, buildData.exceptionLevel);
            PlayerSettings.SetStackTraceLogType(LogType.Assert, buildData.assertLevel);
        }
        
        public void UpdateBuildOptions(UniBuildConfigurationData buildData, IArgumentsProvider arguments)
        {
            SetBuildOptions(buildData.buildOptions, true);
            
            if (buildData.runOnBuildFinish)
            {
                SetBuildOptions(BuildOptions.AutoRunPlayer, false);
            }
            if (buildData.developmentBuild)
            {
                SetBuildOptions(BuildOptions.Development, false);
            }
            if (buildData.autoconnectProfiler)
            {
                SetBuildOptions(BuildOptions.ConnectWithProfiler, false);
            }
            if (buildData.deepProfiling)
            {
                SetBuildOptions(BuildOptions.EnableDeepProfilingSupport, false);
            }
            if (buildData.scriptDebugging)
            {
                SetBuildOptions(BuildOptions.AllowDebugging, false);
            }
        }

        public void UpdateBuildOptionsArguments(IArgumentsProvider arguments)
        {
            arguments.GetStringValue(BuildArguments.BuildOptions, out var options);
            if (string.IsNullOrEmpty(options)) return;
            var optionsArray = options.Split(',');
            foreach (var option in optionsArray)
            {
                if (Enum.TryParse(option, out BuildOptions buildOption))
                {
                    SetBuildOptions(buildOption, false);
                }
            }
            if (Enum.TryParse(options, out BuildOptions singleOption))
            {
                SetBuildOptions(singleOption, false);
            }
            
            if (arguments.GetBoolValue(BuildArguments.DevelopmentBuild, out var devBuild))
                SetBuildOptions(BuildOptions.Development, false);
            if (arguments.GetBoolValue(BuildArguments.AutoConnectProfiler, out var autoConnectProfiler))
                SetBuildOptions(BuildOptions.Development, false);
            if (arguments.GetBoolValue(BuildArguments.DeepProfiling, out var profiling))
                SetBuildOptions(BuildOptions.EnableDeepProfilingSupport, false);
            if (arguments.GetBoolValue(BuildArguments.ScriptDebugging, out var allowDebugging))
                SetBuildOptions(BuildOptions.Development, false);
        }
        
        public void UpdateWebGLData(IArgumentsProvider arguments)
        {
#if UNITY_WEBGL
            if(arguments.GetEnumValue<WasmCodeOptimization>(BuildArguments.WebCodeOptimization, out var branchValue))
                webGlBuildData.CodeOptimization = branchValue;
#endif
            if(arguments.GetBoolValue(BuildArguments.WebDataCaching, out var caching,webGlBuildData.DataCaching))
                webGlBuildData.DataCaching = caching;
            if(arguments.GetEnumValue<WebGLCompressionFormat>(BuildArguments.WebCompressionFormat, out var compressionFormat))
                webGlBuildData.CompressionFormat = compressionFormat;
            if(arguments.GetIntValue(BuildArguments.WebMemorySize, out var memorySize))
                webGlBuildData.MaxMemorySize = memorySize;
            if(arguments.GetBoolValue(BuildArguments.WebShowDiagnostics, out var showDiagnostics))
                webGlBuildData.ShowDiagnostics = showDiagnostics;
        }
        
        public void UpdateWebGLData(WebGlBuildData buildData)
        {
            webGlBuildData = buildData;
            PlayerSettings.WebGL.showDiagnostics = webGlBuildData.ShowDiagnostics;
            PlayerSettings.WebGL.compressionFormat = webGlBuildData.CompressionFormat;
            PlayerSettings.WebGL.memorySize = webGlBuildData.MaxMemorySize;
            PlayerSettings.WebGL.dataCaching = webGlBuildData.DataCaching;
            PlayerSettings.WebGL.debugSymbolMode = webGlBuildData.DebugSymbolMode;
            PlayerSettings.WebGL.exceptionSupport = webGlBuildData.ExceptionSupport;
            PlayerSettings.WebGL.linkerTarget = webGlBuildData.LinkerTarget;
            
            PlayerSettings.defaultWebScreenWidth = webGlBuildData.Resolution.x;
            PlayerSettings.defaultWebScreenHeight = webGlBuildData.Resolution.y;
            
#if UNITY_WEBGL
            UnityEditor.WebGL.UserBuildSettings.codeOptimization = buildData.CodeOptimization;
#endif
        }

        public void SetProductName(string product,UniBuildConfigurationData buildData,IArgumentsProvider arguments)
        {
            if (buildData.overrideProductName && !string.IsNullOrEmpty(buildData.productName))
                productName = buildData.productName;
            
            if(arguments.GetStringValue(BuildArguments.ProductNameKey,out var branchValue))
                productName = branchValue;
            
            PlayerSettings.productName = productName;
        }
        
        public void UpdateArguments(IArgumentsProvider arguments)
        {
            UpdateBuildOptionsArguments(arguments);
            
            if(arguments.GetStringValue(BuildArguments.GitBranchKey,out var branchValue))
                branch = branchValue;

            bundleVersion = PlayerSettings.bundleVersion;
            if(arguments.GetStringValue(BuildArguments.BundleVersionKey,out var bundleVersionValue))
                bundleVersion = branchValue;
            
            if (arguments.GetStringValue(BuildArguments.Linux64BuildTargetKey, out var linuxPath))
            {
                buildTarget = BuildTarget.StandaloneLinux64;
                buildTargetGroup = BuildTargetGroup.Standalone;
                outputFolder = linuxPath;
            }
            
            if (arguments.GetStringValue(BuildArguments.LinuxUniversalBuildTargetKey, out var universalPath))
            {
                buildTarget = BuildTarget.StandaloneLinux64;
                buildTargetGroup = BuildTargetGroup.Standalone;
                outputFolder = universalPath;
            }
            
            if (arguments.GetStringValue(BuildArguments.Windows64PlayerBuildTargetKey, out var windowsPath))
            {
                buildTarget = BuildTarget.StandaloneWindows64;
                buildTargetGroup = BuildTargetGroup.Standalone;
                outputFolder = windowsPath;
            }

            if (arguments.GetEnumValue<StandaloneBuildSubtarget>(
                    BuildArguments.StandaloneBuildSubTargetKey,out var subTarget))
            {
                standaloneBuildSubtarget = subTarget;
            }
            
            if (arguments.GetEnumValue<BuildEnvironmentType>(
                    BuildArguments.BuildEnvironmentType,out var buildEnvironmentValue))
            {
                environmentType = buildEnvironmentValue;
            }

            if (arguments.GetEnumValue<BuildTarget>(BuildArguments.BuildTargetKey,
                    out var buildTargetValue))
            {
                buildTarget = buildTargetValue;
            }
            
            if (arguments.GetEnumValue<BuildTargetGroup>(BuildArguments.BuildTargetGroupKey,
                    out var buildTargetGroupValue))
            {
                buildTargetGroup = buildTargetGroupValue;
            }
            
            if (arguments.GetStringValue(BuildArguments.BundleIdKey,
                    out var bundleIdValue))
            {
                bundleId = bundleIdValue;
            }
            
            if (arguments.GetStringValue(BuildArguments.BuildOutputNameKey,
                    out var outputFileValue))
            {
                outputFile = outputFileValue;
            }
            
            if(arguments.GetEnumValue<ScriptingImplementation>(BuildArguments.ScriptingImplementationKey,out var scripting))
                scriptingImplementation = scripting;

            if (arguments.GetIntValue(BuildArguments.BuildNumberKey, out var version))
            {
                buildNumber = version;
            }

            arguments.GetStringValue(BuildArguments.BuildOutputFolderKey,
                out var folder, BuildFolder);
            outputFolder = folder;

            arguments.GetStringValue(BuildArguments.BuildOutputNameKey, out var file, string.Empty);
            outputFile = file;
            
            arguments.SetValue(BuildArguments.BuildNumberKey, buildNumber.ToString());
            arguments.SetValue(BuildArguments.BuildOutputFolderKey, outputFolder);
            arguments.SetValue(BuildArguments.BuildOutputNameKey, outputFile);
            
            //EditorUserBuildSettings.SwitchActiveBuildTarget(buildTargetGroup,buildTarget);
        }
        
        public void SetBuildOptions(BuildOptions targetBuildOptions, bool replace = true)
        {
            if (replace) {
                buildOptions = targetBuildOptions;
                return;
            }

            buildOptions |= targetBuildOptions;
            
        }
        
    }
}
