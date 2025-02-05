# Unity Asset Generator System

A powerful system for managing game data with clean separation between data models and Unity's serialization layer. This system allows you to define your game data models independently of Unity, making them reusable in other contexts like game servers or tools.

## Overview

The Unity Asset Generator System provides an automated pipeline for converting plain C# data classes into Unity ScriptableObject assets while maintaining a clean separation of concerns. This separation allows the data layer to be completely independent of Unity, making it possible to reuse the same data models in different contexts, such as:

- Game client (Unity)
- Game server
- Tools and utilities
- Data validation services
- Test environments

## Key Features

- ✨ Clean separation between data models and Unity dependencies
- 🔄 Automatic ScriptableObject generation from data classes
- 🏗️ Custom Unity editor tools for asset management
- 🔗 Proper handling of references and inheritance
- 🧹 Built-in cleanup and regeneration tools
- 📦 Support for complex data structures and relationships

## Architecture

### Project Structure

```
Assets/Scripts/
├── Common/               # Core data definitions
│   ├── GameData.cs      # Data container classes
│   └── Attributes/      # Custom attributes
├── Editor/              # Unity editor tools
│   ├── AssetBuilderWindow.cs    
│   └── AssetClassGenerator.cs   
└── Generated/           # Auto-generated Unity assets
    ├── ItemAsset.cs
    ├── WeaponAsset.cs
    └── SpecialEffectAsset.cs
```

### Components

#### 1. Data Layer (`Common/`)
Pure C# classes that define your game data structures. These classes are completely independent of Unity and can be reused in any .NET environment.

```csharp
[GenerateAsset("WeaponAsset")]
[GameDataArray("Weapons")]
public class Weapon : Item
{
    public float BaseDamage { get; set; }
    public DamageType DamageType { get; set; }
    public float AttackSpeed { get; set; }
    public List<SpecialEffect> Effects { get; set; }
}
```

#### 2. Asset Generation (`Editor/`)
Unity editor tools that handle the conversion of data classes into Unity-compatible ScriptableObjects.

- `AssetClassGenerator`: Generates Unity-compatible asset classes
- `AssetBuilderWindow`: Provides UI for managing asset generation

#### 3. Generated Assets (`Generated/`)
Auto-generated Unity ScriptableObject classes that wrap the data classes, handling Unity serialization and asset references.

## How It Works

1. **Data Definition**
   - Define your data structures using plain C# classes
   - Use attributes to mark classes for asset generation:
     - `[GenerateAsset]`: Marks a class for ScriptableObject generation
     - `[GameDataArray]`: Specifies the array name in GameData class

2. **Asset Generation**
   - The system automatically generates ScriptableObject wrapper classes
   - Handles proper serialization of all properties
   - Maintains inheritance hierarchies
   - Manages references between assets

3. **Asset Management**
   - Use the Asset Builder window in Unity
   - Select which asset types to generate
   - Clean and rebuild assets as needed

## Usage

### 1. Define Data Classes

```csharp
// Pure C# data class - no Unity dependencies
[GenerateAsset("ItemAsset")]
public class Item
{
    public string Name { get; set; }
    public string Description { get; set; }
    public ItemRarity Rarity { get; set; }
    public int RequiredLevel { get; set; }
    public List<StatModifier> Stats { get; set; }
}
```

### 2. Generate Asset Classes

1. Open Unity Editor
2. Navigate to Tools > Asset Builder
3. Click "Generate Asset Classes"
4. Wait for Unity to recompile

### 3. Build Assets

1. In the Asset Builder window, select which asset types to build
2. Click "Build Assets"
3. Assets will be generated in your specified output folder

### 4. Use in Server Code

```csharp
// Server-side code - no Unity dependencies
public class GameServer
{
    private List<Weapon> weapons;

    public void LoadWeapons()
    {
        weapons = GameData.Weapons.ToList();
        // Process weapons without any Unity dependencies
    }
}
```

## Benefits

### 1. Clean Architecture
- Clear separation between data and Unity-specific code
- Improved testability
- Better code organization
- Reduced coupling

### 2. Code Reusability
- Use the same data models across different platforms
- Share code between client and server
- Create tools and utilities using the same models

### 3. Maintainability
- Centralized data management
- Automated asset generation
- Reduced boilerplate code
- Type-safe references

### 4. Flexibility
- Easy to extend with new data types
- Support for complex data relationships
- Automated handling of Unity serialization

## Best Practices

1. **Keep Data Classes Pure**
   - Avoid Unity dependencies in data classes
   - Use basic C# types and collections
   - Keep business logic separate from data structures

2. **Use Proper Naming**
   - Follow consistent naming conventions
   - Use meaningful class and property names
   - Document complex relationships

3. **Manage Dependencies**
   - Use proper inheritance hierarchies
   - Keep reference chains manageable
   - Consider data relationships when designing structures

4. **Regular Maintenance**
   - Clean and rebuild assets periodically
   - Update generated classes when data structures change
   - Monitor for any reference issues

## Technical Requirements

- Unity 2020.3 or higher
- .NET Standard 2.0 compatible
- Editor scripting enabled