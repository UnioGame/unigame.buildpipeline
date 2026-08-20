# UniGame.BuildPipeline

Command-based scriptable build pipeline for Unity Engine

![Build Pipeline Editor](https://i.ibb.co/bgWQMYdB/build-pipeline4.png)

- [UniGame.UniBuild](#unigameunibuild)
  - [Quick Start](#quick-start)
    - [Opening Build Pipeline Editor](#opening-build-pipeline-editor)
    - [Creating Your First Pipeline](#creating-your-first-pipeline)
    - [Basic Usage](#basic-usage)
  - [Overview](#overview)
  - [Installation](#installation)
    - [Dependencies](#dependencies)
    - [Package Installation](#package-installation)
    - [Unity Build Profiles Support](#unity-build-profiles-support)
  - [Core Architecture](#core-architecture)
    - [Build System Components](#build-system-components)
    - [Build Pipeline Flow](#build-pipeline-flow)
  - [Command System](#command-system)
    - [Command Types](#command-types)
    - [Creating Commands](#creating-commands)
      - [Simple Serializable Command](#simple-serializable-command)
      - [ScriptableObject Command](#scriptableobject-command)
    - [Built-in Commands](#built-in-commands)
    - [Adding Command Metadata](#adding-command-metadata)
  - [Build Pipeline](#build-pipeline)
    - [Pipeline Execution](#pipeline-execution)
    - [Command Groups](#command-groups)
  - [Console Arguments](#console-arguments)
  - [Examples](#examples)
    - [Example 1: Custom Setup Command](#example-1-custom-setup-command)
  - [Best Practices](#best-practices)

## Quick Start

### Opening Build Pipeline Editor

1. Open Unity Editor
2. Go to menu: **Tools → Build Pipeline → Pipeline Editor**
3. The Build Pipeline Editor window will open

### Creating Your First Pipeline

1. In Project window, right-click and select: **Create → UniBuild → UniBuild Pipeline**
2. Name it (e.g., "AndroidBuild")
3. Open the Build Pipeline Editor (**Tools → Build Pipeline → Pipeline Editor**)
4. Select your new configuration from the pipeline list (left panel)
5. Click **+ Add Step** to start adding commands
6. Choose from available commands:
   - For Android: ApplyAndroidSettingsCommand, SwitchActiveBuildTargetCommand
   - For General: ApplyBuildArgumentsCommand, BuildOptionsCommand
   - For Post-Build: Copy files, upload artifacts, etc.

### Basic Usage

1. **Configure Build Target**
   - Select the platform (Android, iOS, WebGL, etc.)
   - Configure platform-specific settings
   - (Optional) Use Unity Build Profiles for predefined platform configurations

2. **Add Pre-Build Commands**
   - Set up build environment
   - Apply build arguments and settings
   - Configure platform-specific options

3. **Execute Pipeline**
   - In Pipeline Editor, click "Run" button or use menu: **Build → Run Build Configuration**
   - Monitor progress in Console window
   - Pipeline respects active Build Profile settings

4. **Add Post-Build Commands**
   - Copy artifacts to specific locations
   - Trigger deployment processes
   - Generate reports

## Overview

UniGame.UniBuild is a comprehensive build automation system for Unity that provides:

- **Command-Based Architecture**: Modular build steps using command pattern
- **Visual Pipeline Editor**: Drag-drop UI for configuring build steps
- **Multi-Platform Support**: Unified build system for all Unity platforms
- **Unity Build Profiles Support**: Full integration with Unity 6 Build Profiles system
- **Console Integration**: Full command-line interface for CI/CD systems
- **Extensible Commands**: Rich set of built-in commands and easy custom command creation

## Installation

### Dependencies

**Optional for enhanced Inspector:**

- [Odin Inspector](https://odininspector.com) - Advanced inspector features
- [Tri-Inspector](https://github.com/codewriter-packages/Tri-Inspector) - Lightweight alternative

Wuthout these, you can use Pipeline Editor Window or default Unity Inspector for editing pipelines.

### Package Installation

Add to your project manifest (`Packages/manifest.json`):

```json
{
  "dependencies": {
    "com.unigame.unibuildpipeline": "https://github.com/UnioGame/unigame.buildpipeline.git",
  }
}
```

### Unity Build Profiles Support

UniGame.UniBuild fully supports **Unity 6 Build Profiles** system:

- **Profile Integration**: Each Build Profile can have its own pipeline configuration
- **Automatic Activation**: Build Profiles are automatically activated when executing pipelines
- **Profile-native Builds**: Profile pipelines use Unity's `BuildPlayerWithProfileOptions` API
- **Profile-Specific Commands**: Configure different commands for different profiles
- **Consistent Naming**: Pipeline configurations follow Build Profile naming conventions
- **Seamless Workflow**: Works alongside Unity's native Build Profile system without conflicts

**To use with Build Profiles:**

1. Create or select a Build Profile in Unity Editor (Window → Build Profile)
2. Create a pipeline configuration that matches your profile strategy
3. Configure commands to work with the active Build Profile
4. When running the pipeline, UniBuild activates the linked profile and automatically continues after any domain reload

In batch mode, start Unity with the required `-activeBuildProfile`. UniBuild does not switch profiles from inside a synchronous batch build.

## Core Architecture

### Build System Components

The build system consists of key components:

```csharp
// Main build configuration
public interface IUniBuildCommandsMap
{
    bool PlayerBuildEnabled { get; }
    IEnumerable<IUnityBuildCommand> PreBuildCommands { get; }
    IEnumerable<IUnityBuildCommand> PostBuildCommands { get; }
}

// Base command interface
public interface IUnityBuildCommand
{
    string Name { get; }
    void Execute(IUniBuilderConfiguration configuration);
    bool Validate(IUniBuilderConfiguration config);
}
```

**Ready-to-use Base Classes:**

UniGame.UniBuild provides two convenient base classes for implementing commands:

1. **SerializableBuildCommand** - For inline serializable commands

2. **UnityBuildCommand** - For ScriptableObject-based reusable commands

Both classes implement `IUnityBuildCommand` and handle all the boilerplate, allowing you to focus on the command logic in the `Execute()` method.

### Build Pipeline Flow

```
Start Build
    ↓
Initialize Configuration
    ↓
Execute Pre-Build Commands (in order)
    ↓
Execute Unity Build (if enabled)
    ↓
Execute Post-Build Commands (in order)
    ↓
Generate Build Report
    ↓
Finish Build
```

## Command System

### Command Types

UniBuild supports:

1. **SerializableBuildCommand**: Inline commands, serialized in pipeline
2. **UnityBuildCommand**: ScriptableObject-based reusable commands
3. **IUnityBuildCommand**: Base interface implemented by all commands

### Creating Commands

#### Simple Serializable Command

```csharp
using UniGame.UniBuild.Editor.Commands;
using UnityEngine;

[System.Serializable]
public class PrintBuildInfoCommand : SerializableBuildCommand
{
    public string messagePrefix = "Build Info: ";
    
    public override void Execute(IUniBuilderConfiguration configuration)
    {
        var target = configuration.BuildParameters.buildTarget;
        var output = configuration.BuildParameters.outputFolder;
        
        Debug.Log($"{messagePrefix}Target={target}, Output={output}");
    }
}
```

Add to pipeline:
1. Open Pipeline Editor
2. Click "+ Add Step"
3. Select "Print Build Info Command"
4. Configure the prefix text if needed

#### ScriptableObject Command

Create a reusable command asset:

```csharp
using UniGame.UniBuild.Editor.Commands;
using UnityEngine;

[CreateAssetMenu(menuName = "UniBuild/Commands/DeployCommand")]
public class DeployCommand : UnityBuildCommand
{
    [SerializeField] private string deployPath = "Builds/";
    [SerializeField] private bool deleteOld = true;
    
    public override void Execute(IUniBuilderConfiguration configuration)
    {
        if (deleteOld && System.IO.Directory.Exists(deployPath))
            System.IO.Directory.Delete(deployPath, true);
        
        BuildLogger.Log($"Deployed to: {deployPath}");
    }
}
```

Create asset:
1. Right-click in Project
2. **Create → UniBuild → Commands → Deploy Command**
3. Drag-drop the asset into a pipeline step

### Built-in Commands

Common pre-built commands available:

**Platform Configuration**
- `ApplyAndroidSettingsCommand` - Android build settings
- `ApplyWebGLSettingsCommand` - WebGL configuration
- `SwitchActiveBuildTargetCommand` - Change build target

**Build Setup**
- `ApplyBuildArgumentsCommand` - Apply command-line arguments
- `BuildOptionsCommand` - Set Unity BuildOptions
- `SetScriptingBackendCommand` - IL2CPP or Mono
- `ApplyArtifactNameCommand` - Output filename

**Asset Management**
- `ReimportAssetsCommand` - Reimport selected assets
- `ApplyScriptingDefineSymbolsCommand` - Manage #define symbols

**View All Commands in Pipeline Editor**

Open the **Commands Catalog** tab in the Build Pipeline Editor to browse all available commands with descriptions:

![Commands Catalog](https://i.ibb.co/zVQ9sv12/commands-info.png)

The Commands Catalog shows:
- All available commands with their metadata
- Command descriptions and categories
- Search functionality to find specific commands
- Real-time command discovery

### Adding Command Metadata

Make your custom commands discoverable in the Commands Catalog by using the `BuildCommandMetadataAttribute`:

```csharp
using UniGame.UniBuild.Editor.Inspector;
using UnityEngine;

[BuildCommandMetadata(
    displayName: "Deploy to Server",
    description: "Uploads the build artifacts to the deployment server",
    category: "Deployment",
    iconPath: "Assets/Icons/deploy.png"
)]
[System.Serializable]
public class DeployToServerCommand : SerializableBuildCommand
{
    [SerializeField] private string serverUrl = "ftp://deploy.example.com";
    [SerializeField] private bool deleteOldBuilds = true;
    
    public override void Execute(IUniBuilderConfiguration configuration)
    {
        // Your deployment logic here
        BuildLogger.Log($"Deploying to: {serverUrl}");
    }
}
```

**BuildCommandMetadataAttribute Parameters:**
- `displayName` - Human-readable command name shown in UI
- `description` - Detailed description of what the command does
- `category` - Category for organizing commands (e.g., "Deployment", "Platform", "Assets")
- `iconPath` - Optional path to icon asset for visual identification

The metadata is automatically used when:
- Adding steps in the Pipeline Editor
- Displaying commands in the Commands Catalog
- Building the command discovery system

## Build Pipeline

### Pipeline Execution

Pipelines automatically:

1. **Validate all commands** before execution
2. **Execute pre-build commands** in order (top to bottom)
3. **Build Unity player** (if enabled)
4. **Execute post-build commands** in order
5. **Generate build report** with execution times

### Command Groups

Group related commands for organization:

```csharp
[CreateAssetMenu(menuName = "UniBuild/Create CommandsGroup")]
public class PipelineCommandsGroup : UnityBuildCommand
{
    public BuildCommands commands = new BuildCommands();
    
    public override void Execute(IUniBuilderConfiguration configuration)
    {
        foreach (var command in commands.Commands)
        {
            if (command.IsActive)
                command.Execute(configuration);
        }
    }
}
```

Use in pipeline:
1. Create group asset: **Create → UniBuild → Create CommandsGroup**
2. Add as a step in pipeline
3. Expand the group to add nested commands
4. Commands in group execute together

## Console Arguments

Common arguments:
- `-buildTarget` - Build target (Android, iOS, WebGL, etc.)
- `-bundleVersion` - Version string
- `-buildnumber` - Build number
- `-outputFolder` - Output directory
- `-outputFileName` - Output filename
- `-developmentBuild` - Development build flag
- `-gitBranch` - Git branch name

## Examples

### Example 1: Custom Setup Command

```csharp
[System.Serializable]
public class SetupProjectCommand : SerializableBuildCommand
{
    [SerializeField] private bool clearCache = true;
    
    public override bool Validate(IUniBuilderConfiguration config)
    {
        if (config.BuildParameters.buildTarget == BuildTarget.NoTarget)
        {
            Debug.LogError("Build target not set!");
            return false;
        }
        return true;
    }
    
    public override void Execute(IUniBuilderConfiguration configuration)
    {
        var target = configuration.BuildParameters.buildTarget;
        Debug.Log($"Setting up project for {target}");
        
        if (clearCache)
            System.IO.Directory.Delete("Library/ScriptAssemblies", true);
        
        AssetDatabase.Refresh();
    }
}
```

## Best Practices

1. **Keep Commands Simple** - Each command should do one thing well
2. **Validate Input** - Always validate configuration before executing
3. **Use Logging** - Use `BuildLogger.Log()` for debugging and reporting
4. **Group Related Commands** - Use command groups for organization
5. **Test Locally** - Test build pipelines locally before CI/CD integration
6. **Document Parameters** - Add [Tooltip] attributes to command properties
7.  **Order Matters** - Arrange commands logically (setup before build, deploy after)
