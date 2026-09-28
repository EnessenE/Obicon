# Agent Guidelines for Obicon Project

## Project References

- **Todo List:** See `/Project/todo.md` for current task tracking
- **Documentation:** See `/docs/` for API specs and project documentation

## Model Documentation Standard

**Every model field must have a documentation comment** explaining its purpose in max 2 sentences. 

For complex objects (nested types, collections, enums), also include a default value where applicable.

### Example Format

```csharp
/// <summary>
/// Unique identifier for the node. Generated automatically on creation.
/// </summary>
public Guid Id { get; set; }

/// <summary>
/// Human-readable name of the node. Default: empty string.
/// </summary>
public string Name { get; set; } = string.Empty;

/// <summary>
/// Authentication token for WebSocket connections. 
/// Generated as GUID on node creation. Default: empty string.
/// </summary>
public string AuthToken { get; set; } = string.Empty;
```

### Documentation Rules

1. **All public properties** must have `<summary>` documentation
2. **Max 2 sentences** per field - be concise
3. **Complex types** (collections, custom objects, enums): Include default value if applicable
4. **Enums**: Document each value with `<summary>`
5. **DTOs**: Same rules apply - describe what each field represents

### Where This Applies

- All model classes in `Obicon.Server/Models/`
- All DTO classes in `Obicon.Server/Models/Requests/` and `Obicon.Server/Models/Responses/`
- All shared models in `Obicon.Shared/Models/`
- All configuration classes

### Additional Notes

- Use `/// <summary>` XML documentation format
- Place comment **above** the property/field
- No need to document private fields
- For enums: document the enum type and each value
