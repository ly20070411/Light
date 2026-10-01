# 🛠️ Unity MCP Tools Reference

Complete reference for all Unity MCP tools available through any MCP-compatible client.

## 📋 Table of Contents

- [Project Management Tools](#project-management-tools)
- [Scene Operations Tools](#scene-operations-tools)
- [Asset Management Tools](#asset-management-tools)
- [Code Generation Tools](#code-generation-tools)
- [Build & Deploy Tools](#build--deploy-tools)
- [Tool Usage Patterns](#tool-usage-patterns)
- [Parameter Reference](#parameter-reference)

---

## 🏗️ Project Management Tools

### 1. `project.analyze`
**Description**: Comprehensive project analysis and structure overview  
**Unity Tool**: `project_analyze`

**Parameters**:
```json
{
  "includeAssets": false,     // Include detailed asset analysis
  "includePackages": true,    // Include package information
  "includeScenes": true,      // Include scene details
  "includeSettings": true     // Include project settings
}
```

**Example Usage**:
```bash
# Basic project analysis
python3 unity_mcp_client.py project.analyze

# Full analysis with assets
python3 unity_mcp_client.py project.analyze '{"includeAssets": true}'
```

**Example Commands**:
- "Analyze my Unity project structure"
- "Show me project details and settings"
- "Give me a comprehensive project overview"

---

### 2. `project.getInfo`
**Description**: Get basic project information and settings  
**Unity Tool**: `project_getinfo`

**Parameters**: None

**Example Usage**:
```bash
python3 unity_mcp_client.py project.getInfo
```

**Example Commands**:
- "What's the basic info about this project?"
- "Show me project name and version"

---

### 3. `project.setSettings`
**Description**: Modify project configuration and player settings  
**Unity Tool**: `project_setsettings`

**Parameters**:
```json
{
  "companyName": "string",
  "productName": "string",
  "version": "string",
  "bundleVersion": "string",
  "targetPlatform": "string"
}
```

**Example Usage**:
```bash
python3 unity_mcp_client.py project.setSettings '{"companyName": "MyStudio", "productName": "MyGame"}'
```

---

### 4. `project.listScenes`
**Description**: List all scenes in build settings with status  
**Unity Tool**: `project_listscenes`

**Parameters**: None

**Example Usage**:
```bash
python3 unity_mcp_client.py project.listScenes
```

**Example Commands**:
- "List all scenes in the project"
- "Show me the scene hierarchy"

---

### 5. `project.getBuildSettings`
**Description**: Get current build configuration  
**Unity Tool**: `project_getbuildsettings`

**Parameters**: None

**Example Usage**:
```bash
python3 unity_mcp_client.py project.getBuildSettings
```

---

### 6. `project.setBuildTarget`
**Description**: Change active build target platform  
**Unity Tool**: `project_setbuildtarget`

**Parameters**:
```json
{
  "target": "StandaloneWindows64|Android|iOS|WebGL"
}
```

---

### 7. `project.refreshAssets`
**Description**: Force refresh of project assets database  
**Unity Tool**: `project_refreshassets`

**Parameters**: None

---

## 🎬 Scene Operations Tools

### 8. `scene.createGameObject`
**Description**: Create GameObjects with components and hierarchy  
**Unity Tool**: `scene_creategameobject`

**Parameters**:
```json
{
  "name": "string",
  "primitive": "Cube|Sphere|Capsule|Cylinder|Plane|Quad",
  "position": {"x": 0, "y": 0, "z": 0},
  "rotation": {"x": 0, "y": 0, "z": 0},
  "scale": {"x": 1, "y": 1, "z": 1},
  "parent": "string",
  "components": ["Rigidbody", "Collider", "MeshRenderer"]
}
```

**Example Usage**:
```bash
# Create a simple cube
python3 unity_mcp_client.py scene.createGameObject '{"name": "TestCube", "primitive": "Cube"}'

# Create with physics
python3 unity_mcp_client.py scene.createGameObject '{"name": "PhysicsCube", "primitive": "Cube", "components": ["Rigidbody"]}'
```

**Example Commands**:
- "Create a cube at position (0, 5, 0)"
- "Add a sphere with physics components"
- "Create a player character object"

---

### 9. `scene.modifyComponent`
**Description**: Add, remove, or update component properties  
**Unity Tool**: `scene_modifycomponent`

**Parameters**:
```json
{
  "gameObjectName": "string",
  "componentType": "string",
  "action": "add|remove|modify",
  "properties": {}
}
```

---

### 10. `scene.query`
**Description**: Query scene hierarchy and find objects by criteria  
**Unity Tool**: `scene_query`

**Parameters**:
```json
{
  "searchTerm": "string",
  "includeInactive": false,
  "filterByComponent": "string",
  "filterByTag": "string"
}
```

---

### 11. `scene.selectObjects`
**Description**: Select and focus objects in Scene/Hierarchy view  
**Unity Tool**: `scene_selectobjects`

**Parameters**:
```json
{
  "objectNames": ["string"],
  "focusInSceneView": true
}
```

---

### 12. `scene.deleteGameObject`
**Description**: Remove GameObjects and children safely  
**Unity Tool**: `scene_deletegameobject`

**Parameters**:
```json
{
  "gameObjectName": "string",
  "deleteChildren": true
}
```

---

### 13. `scene.moveGameObject`
**Description**: Transform position, rotation, scale, and parenting  
**Unity Tool**: `scene_movegameobject`

**Parameters**:
```json
{
  "gameObjectName": "string",
  "position": {"x": 0, "y": 0, "z": 0},
  "rotation": {"x": 0, "y": 0, "z": 0},
  "scale": {"x": 1, "y": 1, "z": 1},
  "newParent": "string"
}
```

---

### 14. `scene.save`
**Description**: Save current scene or all open scenes  
**Unity Tool**: `scene_save`

**Parameters**:
```json
{
  "saveAll": false,
  "scenePath": "string"
}
```

---

### 15. `scene.load`
**Description**: Open and switch between project scenes  
**Unity Tool**: `scene_load`

**Parameters**:
```json
{
  "scenePath": "string",
  "additive": false
}
```

### 16. `scene.capture`
**Description**: Capture a screenshot of the Unity Scene or Game view for visual inspection
**Unity Tool**: `scene_capture`

**Parameters**:
```json
{
  "view": "game",          // "game" (camera view) or "scene" (editor view)
  "width": 960,            // Image width in pixels (64-3840)
  "height": 540,           // Image height in pixels (64-2160)
  "cameraName": "",        // Specific camera name (empty = Main Camera)
  "includeGizmos": false   // Include gizmos in scene view capture
}
```

**Returns**: PNG image (as MCP image content) + metadata (camera position, FOV, scene view pivot, etc.)

**Example Usage**:
```
"Capture the game view so I can see how the scene looks"
"Take a screenshot of the scene view at 1920x1080"
"Show me what the SecurityCamera sees"
```

**Key Features**:
- Returns actual image data that the AI can see and analyze
- Game view renders from any camera (Main Camera by default)
- Scene view captures the editor viewport with optional gizmos
- Includes camera/viewport metadata for spatial context
- Enables visual feedback loops: place objects → capture → adjust → repeat

---

## 🎨 Asset Management Tools

### 16. `assets.import`
**Description**: Import files with custom import settings  
**Unity Tool**: `assets_import`

**Parameters**:
```json
{
  "filePath": "string",
  "importSettings": {},
  "targetFolder": "Assets/"
}
```

---

### 17. `assets.createMaterial`
**Description**: Generate materials with shader properties  
**Unity Tool**: `assets_creatematerial`

**Parameters**:
```json
{
  "materialName": "string",
  "shaderName": "Standard",
  "properties": {
    "albedo": {"r": 1, "g": 1, "b": 1, "a": 1},
    "metallic": 0.0,
    "smoothness": 0.5
  },
  "savePath": "Assets/Materials/"
}
```

**Example Usage**:
```bash
python3 unity_mcp_client.py assets.createMaterial '{"materialName": "RedMetal", "properties": {"albedo": {"r": 1, "g": 0, "b": 0, "a": 1}, "metallic": 0.8}}'
```

---

### 18. `assets.managePrefabs`
**Description**: Create, modify, and instantiate prefab assets  
**Unity Tool**: `assets_manageprefabs`

**Parameters**:
```json
{
  "action": "create|modify|instantiate",
  "prefabName": "string",
  "sourceObject": "string",
  "savePath": "Assets/Prefabs/"
}
```

---

### 19. `assets.organize`
**Description**: Folder operations, moving, and organizing project assets  
**Unity Tool**: `assets_organize`

**Parameters**:
```json
{
  "action": "createFolder|moveAsset|renameAsset",
  "sourcePath": "string",
  "targetPath": "string",
  "folderName": "string"
}
```

---

### 20. `assets.search`
**Description**: Find assets by name, type, or properties  
**Unity Tool**: `assets_search`

**Parameters**:
```json
{
  "searchTerm": "string",
  "assetType": "Texture|Material|Script|Prefab|Audio",
  "searchInFolder": "Assets/"
}
```

---

### 21. `assets.createTexture`
**Description**: Generate procedural textures and import images  
**Unity Tool**: `assets_createtexture`

**Parameters**:
```json
{
  "textureName": "string",
  "width": 512,
  "height": 512,
  "textureType": "procedural|imported",
  "pattern": "solid|noise|gradient|checkerboard"
}
```

---

### 22. `packages.manage`
**Description**: Install, update, and remove Unity packages  
**Unity Tool**: `packages_manage`

**Parameters**:
```json
{
  "action": "install|remove|update|list",
  "packageName": "string",
  "version": "string"
}
```

---

### 23. `assets.getInfo`
**Description**: Get detailed information about selected assets  
**Unity Tool**: `assets_getinfo`

**Parameters**:
```json
{
  "assetPath": "string",
  "includeMetadata": true
}
```

---

## 💻 Code Generation Tools

### 24. `code.createScript`
**Description**: Generate C# scripts from templates or descriptions  
**Unity Tool**: `code_createscript`

**Parameters**:
```json
{
  "scriptName": "string",
  "scriptType": "MonoBehaviour|ScriptableObject|EditorScript|Interface",
  "template": "string",
  "namespace": "string",
  "savePath": "Assets/Scripts/",
  "methods": ["Start", "Update", "FixedUpdate"]
}
```

**Example Usage**:
```bash
python3 unity_mcp_client.py code.createScript '{"scriptName": "PlayerController", "scriptType": "MonoBehaviour", "methods": ["Start", "Update"]}'
```

---

### 25. `code.analyzeScripts`
**Description**: Parse existing scripts and extract information  
**Unity Tool**: `code_analyzescripts`

**Parameters**:
```json
{
  "scriptPath": "string",
  "analyzeReferences": true,
  "checkComplexity": true
}
```

---

### 26. `code.attachScripts`
**Description**: Attach MonoBehaviour scripts to GameObjects  
**Unity Tool**: `code_attachscripts`

**Parameters**:
```json
{
  "gameObjectName": "string",
  "scriptName": "string",
  "scriptParameters": {}
}
```

---

### 27. `code.findReferences`
**Description**: Find script and component usage across project  
**Unity Tool**: `code_findreferences`

**Parameters**:
```json
{
  "targetScript": "string",
  "searchType": "usage|dependencies|inheritance"
}
```

---

### 28. `code.refactor`
**Description**: Rename, move, and restructure code safely  
**Unity Tool**: `code_refactor`

**Parameters**:
```json
{
  "action": "rename|move|extract",
  "targetScript": "string",
  "newName": "string",
  "newPath": "string"
}
```

---

### 29. `code.generateDocumentation`
**Description**: Auto-generate XML documentation  
**Unity Tool**: `code_generatedocumentation`

**Parameters**:
```json
{
  "scriptPath": "string",
  "includePrivateMembers": false,
  "outputFormat": "xml|markdown"
}
```

---

### 30. `code.validate`
**Description**: Check scripts for common issues and best practices  
**Unity Tool**: `code_validate`

**Parameters**:
```json
{
  "scriptPath": "string",
  "checkPerformance": true,
  "checkSecurity": true,
  "checkStyle": true
}
```

---

### 31. `code.format`
**Description**: Format and style code according to conventions  
**Unity Tool**: `code_format`

**Parameters**:
```json
{
  "scriptPath": "string",
  "styleGuide": "unity|microsoft|custom",
  "formatOptions": {}
}
```

---

## 🏗️ Build & Deploy Tools

### 32. `build.configure`
**Description**: Set build settings, scenes, and platform options  
**Unity Tool**: `build_configure`

**Parameters**:
```json
{
  "buildTarget": "StandaloneWindows64|Android|iOS|WebGL",
  "development": false,
  "scriptDebugging": false,
  "scenePaths": ["Assets/Scenes/MainMenu.unity"],
  "buildPath": "Builds/"
}
```

---

### 33. `build.execute`
**Description**: Trigger builds for target platforms  
**Unity Tool**: `build_execute`

**Parameters**:
```json
{
  "buildTarget": "string",
  "buildPath": "string",
  "options": "None|Development|ConnectProfiler"
}
```

---

### 34. `build.runTests`
**Description**: Execute Unity Test Runner and get results  
**Unity Tool**: `build_runtests`

**Parameters**:
```json
{
  "testMode": "EditMode|PlayMode",
  "testAssembly": "string",
  "generateReport": true
}
```

---

### 35. `build.getReport`
**Description**: Get detailed build reports and statistics  
**Unity Tool**: `build_getreport`

**Parameters**:
```json
{
  "buildPath": "string",
  "includeAssetDetails": true,
  "includeSize": true
}
```

---

### 36. `build.clean`
**Description**: Clean build cache and temporary files  
**Unity Tool**: `build_clean`

**Parameters**:
```json
{
  "cleanCache": true,
  "cleanTempFiles": true,
  "cleanBuilds": false
}
```

---

### 37. `build.addressables`
**Description**: Manage Addressable Asset System configuration  
**Unity Tool**: `build_addressables`

**Parameters**:
```json
{
  "action": "build|clean|analyze",
  "buildTarget": "string",
  "profileName": "string"
}
```

---

### 38. `build.optimize`
**Description**: Analyze and optimize build size and performance  
**Unity Tool**: `build_optimize`

**Parameters**:
```json
{
  "optimizationType": "size|performance|memory",
  "generateReport": true
}
```

---

### 39. `build.getConsoleLogs`
**Description**: Retrieve Unity Console messages and errors  
**Unity Tool**: `build_getconsolelogs`

**Parameters**:
```json
{
  "logLevel": "All|Info|Warning|Error",
  "maxEntries": 100,
  "includeStackTrace": false
}
```

---

### 40. `build.profile`
**Description**: Performance profiling and optimization suggestions  
**Unity Tool**: `build_profile`

**Parameters**:
```json
{
  "profileTarget": "CPU|Memory|Rendering|Audio",
  "duration": 10,
  "generateReport": true
}
```

---

## 🎯 Tool Usage Patterns

### Common Workflows

#### 1. **Project Setup**
```bash
# Analyze existing project
python3 unity_mcp_client.py project.analyze

# Set project settings
python3 unity_mcp_client.py project.setSettings '{"companyName": "MyStudio"}'

# Configure build target
python3 unity_mcp_client.py project.setBuildTarget '{"target": "StandaloneWindows64"}'
```

#### 2. **Scene Building**
```bash
# Create base objects
python3 unity_mcp_client.py scene.createGameObject '{"name": "Player", "primitive": "Capsule"}'

# Add components
python3 unity_mcp_client.py scene.modifyComponent '{"gameObjectName": "Player", "action": "add", "componentType": "Rigidbody"}'

# Create materials
python3 unity_mcp_client.py assets.createMaterial '{"materialName": "PlayerMaterial"}'
```

#### 3. **Code Generation**
```bash
# Create scripts
python3 unity_mcp_client.py code.createScript '{"scriptName": "PlayerController", "scriptType": "MonoBehaviour"}'

# Attach to objects
python3 unity_mcp_client.py code.attachScripts '{"gameObjectName": "Player", "scriptName": "PlayerController"}'

# Validate code
python3 unity_mcp_client.py code.validate '{"scriptPath": "Assets/Scripts/PlayerController.cs"}'
```

#### 4. **Build Process**
```bash
# Configure build
python3 unity_mcp_client.py build.configure '{"buildTarget": "StandaloneWindows64", "development": true}'

# Run tests
python3 unity_mcp_client.py build.runTests '{"testMode": "EditMode"}'

# Execute build
python3 unity_mcp_client.py build.execute '{"buildTarget": "StandaloneWindows64"}'
```

### Natural Language Examples

#### **Project Analysis**
- "Analyze my Unity project and show me the key metrics"
- "What packages are installed in this project?"
- "Show me all scenes and their build status"

#### **Scene Creation**
- "Create a 3D platformer level with platforms and obstacles"
- "Add a player character with movement components"
- "Set up a basic UI canvas with buttons"

#### **Asset Management**
- "Create a red metallic material for my car"
- "Import all textures from the Downloads folder"
- "Organize my assets by type into folders"

#### **Code Development**
- "Generate a first-person camera controller script"
- "Create an enemy AI script with patrol behavior"
- "Find all scripts that reference the GameManager"

#### **Build & Deploy**
- "Configure the project for mobile Android build"
- "Run all unit tests and show me the results"
- "Build the project for Windows with development settings"

---

## 📚 Parameter Reference

### Common Parameter Types

#### **Vector3**
```json
{"x": 0.0, "y": 0.0, "z": 0.0}
```

#### **Color**
```json
{"r": 1.0, "g": 1.0, "b": 1.0, "a": 1.0}
```

#### **Build Targets**
- `StandaloneWindows64`
- `StandaloneOSX`  
- `StandaloneLinux64`
- `Android`
- `iOS`
- `WebGL`
- `tvOS`
- `Switch`

#### **Component Types**
- `Transform`
- `Rigidbody`
- `Collider`
- `MeshRenderer`
- `MeshFilter`
- `Camera`
- `Light`
- `AudioSource`
- `ParticleSystem`
- `Canvas`
- `Image`
- `Text`
- `Button`

#### **Script Types**
- `MonoBehaviour` - Standard Unity component script
- `ScriptableObject` - Data container script  
- `EditorScript` - Unity Editor extension
- `Interface` - C# interface definition
- `StaticClass` - Utility class with static methods

### Error Handling

All tools return standardized responses:

#### **Success Response**
```json
{
  "Id": "request-id",
  "Type": "response", 
  "Result": {
    "success": true,
    "message": "Operation completed successfully",
    "data": { /* tool-specific data */ }
  },
  "Error": null
}
```

#### **Error Response**
```json
{
  "Id": "request-id",
  "Type": "response",
  "Result": null,
  "Error": {
    "Code": 500,
    "Message": "Tool execution failed: specific error message",
    "Data": { /* additional error context */ }
  }
}
```

---

**📝 Note**: This reference covers all 40 tools registered in the Unity MCP server. Tool availability depends on Unity Editor being running with the MCP Bridge plugin active. 