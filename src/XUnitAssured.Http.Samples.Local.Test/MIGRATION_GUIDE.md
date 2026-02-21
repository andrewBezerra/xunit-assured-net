# Migration Guide: Legacy AssertJsonPath to New Syntax

## Overview
The `AssertJsonPath` method has been improved to use strongly-typed generics properly.
The old syntax with `Func<object?, bool>` still works but now requires explicit type parameters.

## Quick Fix for Compilation Errors

### ❌ Old Code (now requires type parameter):
```csharp
.AssertJsonPath("$.id", value => (int)value == 1, "ID should be 1")
```

### ✅ New Code (Option 1 - Add type parameter):
```csharp
.AssertJsonPath<int>("$.id", value => value == 1, "ID should be 1")
```

### ✅ New Code (Option 2 - Use Action<T> with Shouldly):
```csharp
.AssertJsonPath<int>("$.id", id => id.ShouldBe(1))
```

## Migration Examples

### Example 1: String Assertions
```csharp
// OLD
.AssertJsonPath("$.name", value => !string.IsNullOrEmpty(value?.ToString()), "Name required")

// NEW (Option 1)
.AssertJsonPath<string>("$.name", value => !string.IsNullOrEmpty(value), "Name required")

// NEW (Option 2 - Recommended)
.AssertJsonPath<string>("$.name", name => name.ShouldNotBeNullOrEmpty())
```

### Example 2: Int Assertions
```csharp
// OLD
.AssertJsonPath("$.id", value => (int)value > 0, "ID should be positive")

// NEW (Option 1)
.AssertJsonPath<int>("$.id", value => value > 0, "ID should be positive")

// NEW (Option 2 - Recommended)
.AssertJsonPath<int>("$.id", id => id.ShouldBeGreaterThan(0))
```

### Example 3: Decimal Assertions
```csharp
// OLD
.AssertJsonPath("$.price", value => (decimal)((JsonElement)value).GetDecimal() > 0, "Price must be positive")

// NEW (Option 1)
.AssertJsonPath<decimal>("$.price", value => value > 0, "Price must be positive")

// NEW (Option 2 - Recommended)
.AssertJsonPath<decimal>("$.price", price => 
{
    price.ShouldBeGreaterThan(0);
    price.ShouldBeLessThan(10000);
})
```

### Example 4: Bool Assertions
```csharp
// OLD
.AssertJsonPath("$.authenticated", value => (bool)value == true, "Should be authenticated")

// NEW (Option 1)
.AssertJsonPath<bool>("$.authenticated", value => value == true, "Should be authenticated")

// NEW (Option 2 - Recommended)
.AssertJsonPath<bool>("$.authenticated", auth => auth.ShouldBeTrue())
```

### Example 5: Array Length
```csharp
// OLD
.AssertJsonPath("$", value => ((JsonElement)value).GetArrayLength() > 0, "Array should not be empty")

// NEW (Option 1)
.AssertJsonPath<JsonElement>("$", value => value.GetArrayLength() > 0, "Array should not be empty")

// NEW (Option 2 - Recommended)
.AssertJsonPath<JsonElement>("$", array => array.GetArrayLength().ShouldBeGreaterThan(0))
```

## Benefits of New Syntax

### ✅ No More Type Casting
```csharp
// OLD: Manual casting everywhere
.AssertJsonPath("$.price", value => (decimal)((JsonElement)value).GetDecimal() > 0, "...")

// NEW: Clean and type-safe
.AssertJsonPath<decimal>("$.price", price => price.ShouldBeGreaterThan(0))
```

### ✅ Better IntelliSense
```csharp
.AssertJsonPath<string>("$.name", name => 
{
    // IntelliSense shows string methods!
    name.ShouldNotBeNullOrEmpty();
    name.ShouldStartWith("Product");
    name.ShouldContain("Test");
})
```

### ✅ Clearer Error Messages (with Shouldly)
```csharp
// OLD Error:
// JSON path assertion failed for path: $.price

// NEW Error (with Shouldly):
// price should be greater than 0 but was -10
```

## Automated Migration Script

### Find and Replace Pattern (Regex):
```regex
Find:    \.AssertJsonPath\("(\$\.[^"]+)", value => \((\w+)\)value
Replace: .AssertJsonPath<$2>("$1", value => value
```

## Complete Example: Before and After

### Before (Legacy):
```csharp
Given()
    .ApiResource($"{baseUrl}/api/products/1")
    .Get()
.When()
    .Execute()
.Then()
    .AssertJsonPath("$.id", value => (int)value == 1, "ID should be 1")
    .AssertJsonPath("$.name", value => !string.IsNullOrEmpty(value?.ToString()), "Name required")
    .AssertJsonPath("$.price", value => (decimal)((JsonElement)value).GetDecimal() > 0, "Price positive")
    .AssertStatusCode(200);
```

### After (Recommended):
```csharp
Given()
    .ApiResource($"{baseUrl}/api/products/1")
    .Get()
.When()
    .Execute()
.Then()
    // NEW: Validate entire structure first
    .ValidateContract<Product>()
    
    // NEW: Clean, typed assertions
    .AssertJsonPath<int>("$.id", id => id.ShouldBe(1))
    .AssertJsonPath<string>("$.name", name => name.ShouldNotBeNullOrEmpty())
    .AssertJsonPath<decimal>("$.price", price => price.ShouldBeGreaterThan(0))
    .AssertStatusCode(200);
```

## Need Help?

1. **Compilation errors?** Add explicit type parameter `<T>` to your `AssertJsonPath` calls
2. **Want cleaner syntax?** Migrate to `Action<T>` overload with Shouldly assertions
3. **Want automatic validation?** Add `.ValidateContract<YourType>()` before field assertions

## Backward Compatibility

The old `Func<T, bool>` syntax still works! Just add the type parameter:

```csharp
// This still compiles (but not recommended):
.AssertJsonPath<int>("$.id", value => value == 1, "ID should be 1")

// Recommended instead:
.AssertJsonPath<int>("$.id", id => id.ShouldBe(1))
```
