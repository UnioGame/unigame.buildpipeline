using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UniGame.UniBuild.Editor
{
    using System;
    using Abstract;
    using ClientBuild;
    using ClientBuild.BuildConfiguration;
    using UnityEditor;
    using UnityEditor.Build.Profile;
    using UnityEditor.Build.Reporting;
    using UnityEditor.Compilation;
    using Object = Object;

    public enum UniBuildExecutionStatus
    {
        Completed,
        Scheduled,
        Failed
    }

    public readonly struct UniBuildExecutionResult
    {
        public UniBuildExecutionResult(string pipelineName, UniBuildExecutionStatus status,
            bool playerBuildEnabled, BuildReport report = null, Exception exception = null)
        {
            PipelineName = pipelineName;
            Status = status;
            PlayerBuildEnabled = playerBuildEnabled;
            Report = report;
            Exception = exception;
        }

        public string PipelineName { get; }
        public UniBuildExecutionStatus Status { get; }
        public bool PlayerBuildEnabled { get; }
        public BuildReport Report { get; }
        public Exception Exception { get; }
    }

    [InitializeOnLoad]
    public static class UniBuildPipelineTool
    {
        public const string BuildFolder = "Build";

        private const string PendingPipelineGuidKey = "UniBuild.PendingPipelineGuid";
        private const string PendingAutoRunKey = "UniBuild.PendingAutoRun";
        private const string PreviousProfileGuidKey = "UniBuild.PreviousProfileGuid";
        private const string PreviousProfileWasGlobalKey = "UniBuild.PreviousProfileWasGlobal";

        private static UnityPlayerBuilder builder = new UnityPlayerBuilder();
        private static bool restoreAfterCompilation;
        private static bool restoreAfterProjectChange;

        static UniBuildPipelineTool()
        {
            if (HasPendingBuild)
            {
                SchedulePendingBuild();
                return;
            }

            SchedulePreviousBuildProfileRestore();
        }

        public static event Action<UniBuildExecutionResult> ExecutionFinished;

        public static bool HasPendingBuild =>
            !string.IsNullOrEmpty(SessionState.GetString(PendingPipelineGuidKey, string.Empty));
    
        public static EditorBuildConfiguration CreateConfiguration(UniBuildConfigurationData buildData)
        {
            var commandLineParameters = Environment.GetCommandLineArgs().ToList();
            commandLineParameters.Add( $"{BuildArguments.BuildOutputFolderKey}:Builds");
            commandLineParameters.Add( $"{BuildArguments.BuildOutputNameKey}:{buildData.artifactName}");
            
            var argumentsProvider = new ArgumentsProvider(commandLineParameters.ToArray());
            var buildParameters = new BuildParameters(buildData, argumentsProvider);
            var buildConfiguration = new EditorBuildConfiguration(argumentsProvider, buildParameters);
            
            Debug.LogFormat("\nUNIBUILD [CreateConfiguration] {0} \n", argumentsProvider);
            
            return buildConfiguration;
        }
        
                
        public static void BuildByConfigurationId(string guid)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var asset     = AssetDatabase.LoadAssetAtPath<UniBuildPipeline>(assetPath);
            RequestBuild(asset);
        }
        
        public static void BuildAndRunByConfigurationId(string guid)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var asset     = AssetDatabase.LoadAssetAtPath<UniBuildPipeline>(assetPath);
            RequestBuild(asset, true);
        }

        public static UniBuildExecutionResult RequestBuild(UniBuildPipeline pipeline,
            bool autoRun = false)
        {
            if (pipeline == null)
                throw new ArgumentNullException(nameof(pipeline));

            var profile = pipeline.BuildData.useBuildProfile
                ? pipeline.BuildData.buildProfile
                : null;
            var activeProfile = BuildProfile.GetActiveBuildProfile();
            if (profile == null || profile == activeProfile)
                return ExecuteRequestedBuild(pipeline, autoRun);

            if (Application.isBatchMode)
            {
                throw new InvalidOperationException(
                    $"Build Profile `{profile.name}` is not active. Start Unity with -activeBuildProfile before running this pipeline in batch mode.");
            }

            if (HasPendingBuild)
                throw new InvalidOperationException("Another UniBuild pipeline is waiting for a Build Profile switch.");

            var path = AssetDatabase.GetAssetPath(pipeline);
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
                throw new InvalidOperationException("A pipeline must be saved as an asset before switching its Build Profile.");

            SessionState.SetString(PendingPipelineGuidKey, guid);
            SessionState.SetBool(PendingAutoRunKey, autoRun);
            try
            {
                RememberActiveBuildProfile(activeProfile);
                BuildProfile.SetActiveBuildProfile(profile);
                SchedulePendingBuild();
            }
            catch
            {
                ClearPendingBuild();
                ClearPreviousBuildProfile();
                throw;
            }

            Debug.Log($"UniBuild is switching to Build Profile `{profile.name}`. The pipeline will continue after compilation.", pipeline);
            return new UniBuildExecutionResult(pipeline.name, UniBuildExecutionStatus.Scheduled,
                pipeline.PlayerBuildEnabled);
        }
        
        public static BuildReport ExecuteBuild(IUniBuildCommandsMap commandsMap)
        {
            var buildData     = commandsMap.BuildData;
            var profile = buildData.useBuildProfile ? buildData.buildProfile : null;
            if (profile != null && BuildProfile.GetActiveBuildProfile() != profile)
            {
                throw new InvalidOperationException(
                    $"Build Profile `{profile.name}` must be active before executing the pipeline.");
            }
            var configuration = CreateConfiguration(buildData);
            return BuildPlayer(configuration,commandsMap);
        }

        public static void ExecuteCommands(this IEnumerable<IUnityBuildCommand> commands, UniBuildConfigurationData buildData = null)
        {
            buildData ??= new UniBuildConfigurationData()
            {
                buildTarget = EditorUserBuildSettings.activeBuildTarget,
                buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup,
                artifactName = "Empty"
            }; 
            var configuration = CreateConfiguration(buildData);
            builder.ExecuteCommands(commands,configuration);
        }
        
        /// <summary>
        /// Console build call. Close editor after end of build process
        /// </summary>
        public static void BuildUnityPlayer()
        {
            var configuration = new UniBuilderConsoleConfiguration(Environment.GetCommandLineArgs());
        
            var report = BuildPlayer(configuration);

            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        public static BuildReport BuildPlayer(IUniBuilderConfiguration configuration)
        {
            var report = builder.Build(configuration);
            return report;
        }

        public static BuildReport BuildPlayer(IUniBuilderConfiguration configuration, IUniBuildCommandsMap commandsMap)
        {
            var report = builder.Build(configuration,commandsMap);
            return report;
        }

        private static UniBuildExecutionResult ExecuteRequestedBuild(UniBuildPipeline pipeline,
            bool autoRun)
        {
            var pipelineName = pipeline.name;
            var playerBuildEnabled = pipeline.PlayerBuildEnabled;
            IUniBuildCommandsMap commandsMap = pipeline;
            if (autoRun)
            {
                var instance = Object.Instantiate(pipeline);
                instance.BuildData.buildOptions |= BuildOptions.AutoRunPlayer;
                commandsMap = instance;
            }

            var report = ExecuteBuild(commandsMap);
            return new UniBuildExecutionResult(pipelineName,
                UniBuildExecutionStatus.Completed, playerBuildEnabled, report);
        }

        private static void SchedulePendingBuild()
        {
            if (!HasPendingBuild)
                return;
            EditorApplication.delayCall -= ResumePendingBuild;
            EditorApplication.delayCall += ResumePendingBuild;
        }

        private static void ResumePendingBuild()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                BuildPipeline.isBuildingPlayer)
            {
                SchedulePendingBuild();
                return;
            }

            var guid = SessionState.GetString(PendingPipelineGuidKey, string.Empty);
            if (string.IsNullOrEmpty(guid))
                return;

            var autoRun = SessionState.GetBool(PendingAutoRunKey, false);
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var pipeline = AssetDatabase.LoadAssetAtPath<UniBuildPipeline>(path);
            ClearPendingBuild();

            UniBuildExecutionResult result;
            try
            {
                if (pipeline == null)
                    throw new InvalidOperationException($"Pending UniBuild pipeline `{guid}` was not found.");
                result = ExecuteRequestedBuild(pipeline, autoRun);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, pipeline);
                result = new UniBuildExecutionResult(pipeline == null ? guid : pipeline.name,
                    UniBuildExecutionStatus.Failed, pipeline != null && pipeline.PlayerBuildEnabled,
                    exception: exception);
            }

            try
            {
                ExecutionFinished?.Invoke(result);
            }
            finally
            {
                SchedulePreviousBuildProfileRestore();
            }
        }

        private static void RememberActiveBuildProfile(BuildProfile profile)
        {
            if (profile == null)
            {
                SessionState.SetString(PreviousProfileGuidKey, string.Empty);
                SessionState.SetBool(PreviousProfileWasGlobalKey, true);
                return;
            }

            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(profile));
            if (string.IsNullOrEmpty(guid))
                throw new InvalidOperationException(
                    "The active Build Profile must be saved as an asset before UniBuild can restore it.");

            SessionState.SetString(PreviousProfileGuidKey, guid);
            SessionState.SetBool(PreviousProfileWasGlobalKey, false);
        }

        private static void SchedulePreviousBuildProfileRestore()
        {
            if (DeferPreviousBuildProfileRestore())
                return;
            EditorApplication.delayCall -= RestorePreviousBuildProfile;
            EditorApplication.delayCall += RestorePreviousBuildProfile;
        }

        private static void RestorePreviousBuildProfile()
        {
            if (DeferPreviousBuildProfileRestore())
                return;

            var wasGlobal = SessionState.GetBool(PreviousProfileWasGlobalKey, false);
            var guid = SessionState.GetString(PreviousProfileGuidKey, string.Empty);
            BuildProfile profile = null;
            if (!wasGlobal)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(path);
                if (profile == null)
                {
                    Debug.LogWarning(
                        $"Previous UniBuild Build Profile `{guid}` was not found; the active Build Profile was left unchanged.");
                    ClearPreviousBuildProfile();
                    return;
                }
            }

            try
            {
                BuildProfile.SetActiveBuildProfile(profile);
                ClearPreviousBuildProfile();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static bool DeferPreviousBuildProfileRestore()
        {
            if (!HasPreviousBuildProfile() || HasPendingBuild ||
                BuildPipeline.isBuildingPlayer)
                return true;

            if (EditorApplication.isCompiling)
            {
                SubscribeToCompilationFinished();
                return true;
            }

            if (EditorApplication.isUpdating)
            {
                SubscribeToProjectChanged();
                return true;
            }

            return false;
        }

        private static void SubscribeToCompilationFinished()
        {
            if (restoreAfterCompilation)
                return;

            restoreAfterCompilation = true;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
        }

        private static void OnCompilationFinished(object context)
        {
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            restoreAfterCompilation = false;
            SchedulePreviousBuildProfileRestore();
        }

        private static void SubscribeToProjectChanged()
        {
            if (restoreAfterProjectChange)
                return;

            restoreAfterProjectChange = true;
            EditorApplication.projectChanged += OnProjectChanged;
        }

        private static void OnProjectChanged()
        {
            EditorApplication.projectChanged -= OnProjectChanged;
            restoreAfterProjectChange = false;
            SchedulePreviousBuildProfileRestore();
        }

        private static bool HasPreviousBuildProfile() =>
            SessionState.GetBool(PreviousProfileWasGlobalKey, false) ||
            !string.IsNullOrEmpty(SessionState.GetString(PreviousProfileGuidKey, string.Empty));

        private static void ClearPreviousBuildProfile()
        {
            SessionState.EraseString(PreviousProfileGuidKey);
            SessionState.EraseBool(PreviousProfileWasGlobalKey);
        }

        private static void ClearPendingBuild()
        {
            SessionState.EraseString(PendingPipelineGuidKey);
            SessionState.EraseBool(PendingAutoRunKey);
        }
        
    }
}
