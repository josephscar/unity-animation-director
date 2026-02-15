# How to Export Animation Director as a Package

## Method 1: Unity Package (.unitypackage) - Easiest for Distribution

1. **Select the folder**:
   - In Unity Project window, right-click on `Assets/AnimationDirector`
   - Select "Export Package..."

2. **Configure export**:
   - Ensure all files are checked
   - Click "Export..."

3. **Save the package**:
   - Choose a location and name it `AnimationDirector_v1.0.0.unitypackage`
   - Click "Save"

4. **To use in another project**:
   - Open the target Unity project
   - Assets -> Import Package -> Custom Package
   - Select your `.unitypackage` file
   - Click "Import"

## Method 2: Unity Package Manager (UPM) - Modern Approach

### For Git Distribution:

1. **Create a Git repository**:
   ```bash
   cd Assets/AnimationDirector
   git init
   git add .
   git commit -m "Initial release"
   ```

2. **Push to GitHub/GitLab**:
   - Create a repository on GitHub
   - Push your code

3. **Install in other projects**:
   - Window -> Package Manager
   - Click `+` -> "Add package from git URL"
   - Enter: `https://github.com/yourusername/unity-animation-director.git?path=/Assets/AnimationDirector`

### For Local Package:

1. **Copy to Packages folder**:
   - Copy `Assets/AnimationDirector` to `Packages/com.animationdirector.tool`
   - Or create a symlink

2. **Or use as embedded package**:
   - Keep in `Assets/AnimationDirector` (current setup)
   - Works immediately in the same project

## Method 3: Asset Store Submission

1. **Prepare documentation**:
   - Screenshots of the editor window
   - Demo scene showing the tool in action
   - Video tutorial (optional but recommended)

2. **Follow Asset Store guidelines**:
   - Review Unity Asset Store submission requirements
   - Package must be tested and documented
   - Include license information

3. **Submit via Unity Asset Store Publisher Portal**

## Recommended Structure for Distribution

```
AnimationDirector/
├── package.json          (UPM package manifest)
├── README.md             (Documentation)
├── CHANGELOG.md          (Version history)
├── Runtime/              (Runtime scripts)
│   ├── ActionType.cs
│   ├── ActionKeyframe.cs
│   ├── ActionSequencePlayer.cs
│   ├── AnimationActionSequence.cs
│   └── TransformAnchor.cs
└── Editor/               (Editor scripts)
    └── AnimationDirectorWindow.cs
```

## Testing Before Distribution

1. **Test in a clean project**:
   - Create a new Unity project
   - Import the package
   - Verify all features work

2. **Check dependencies**:
   - Ensure no external dependencies required
   - Test with different Unity versions (if supporting multiple)

3. **Documentation**:
   - Update README with accurate information
   - Include example usage
   - Add troubleshooting section

## Version Numbering

Follow semantic versioning: `MAJOR.MINOR.PATCH`
- **MAJOR**: Breaking changes
- **MINOR**: New features (backward compatible)
- **PATCH**: Bug fixes

Update version in:
- `package.json` (version field)
- `CHANGELOG.md`
- Package filename (if using .unitypackage)

