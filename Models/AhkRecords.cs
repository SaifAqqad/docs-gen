namespace docs_gen.Models;

public record AhkClass
{
    public string Name { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string? Extends { get; set; }

    public AhkMethod? Constructor { get; set; }

    public List<AhkMethod> Methods { get; set; } = [];

    public List<AhkProperty> Properties { get; set; } = [];
}

public record AhkMethod
{
    public string Name { get; set; } = null!;

    public string Description { get; set; } = null!;

    public List<AhkParameter> Parameters { get; set; } = [];

    public AhkValue? Returns { get; set; }

    public List<AhkValue> Throws { get; set; } = [];

    public bool Static { get; set; }
}

public record AhkProperty
{
    public string Name { get; set; } = null!;

    public string Description { get; set; } = null!;

    public List<AhkParameter> Parameters { get; set; } = [];

    public string Type { get; set; } = null!;

    public bool Static { get; set; }
}

public record AhkParameter
{
    public string Name { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string? Type { get; set; }

    public bool IsOptional { get; set; }

    public string? DefaultValue { get; set; }

    public override string ToString()
    {
        return $"{Name}{(IsOptional ? $" := {DefaultValue}" : string.Empty)}";
    }
}

public record AhkValue
{
    public string Type { get; set; } = null!;

    public string Description { get; set; } = null!;
}