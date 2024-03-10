using docs_gen.Extensions;
using docs_gen.Models;
using Jint;
using Jint.Native;
using Jint.Native.Function;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

var defaultJsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.General) { WriteIndented = true };
var config = JsonSerializer.Deserialize<Config>(File.ReadAllText("config.json"), defaultJsonOptions)!;
DocStringExtensions.BaseUri = config.BaseUri?.TrimEnd('/', '\\') ?? ".";

var jsEngine = new Engine(options =>
{
    options.EnableModules(@$"{Environment.CurrentDirectory}\js");
});

var jsDocParser = ImportJsDocParser(jsEngine);
var defaultJsDocOptions = JsValue.FromObject(jsEngine, new
{
    spacing = "preserve" // Keep spaces/newlines in the comments
});

if (!File.Exists(config.JsonFile))
{
    Console.WriteLine($"JSON file {config.JsonFile} not found");
    return -1;
}

if (string.IsNullOrWhiteSpace(config.OutputDir))
{
    Console.WriteLine("Output directory not specified in config.json");
    return -2;
}

// Ensure the output directory exists
Directory.CreateDirectory(config.OutputDir);

var ahkJson = JsonNode.Parse(File.ReadAllText(config.JsonFile))!.AsObject();
var ahkClasses = new Dictionary<string, AhkClass>();

// Go through each file in the AHK JSON
foreach (var (_, content) in ahkJson)
{
    // Go through each class in the file
    if (content?["classes"]?.AsArray() is JsonArray classes)
    {
        foreach (var classObj in classes)
        {
            ProcessAhkClass(classObj);
        }
    }
}

if (config.OutputProcessedJson)
{
    var processedJsonPath = Path.ChangeExtension(config.JsonFile, ".processed.json");
    File.WriteAllText(processedJsonPath, JsonSerializer.Serialize(ahkClasses, defaultJsonOptions));
    Console.WriteLine($"Processed JSON written to {processedJsonPath}");
}

await Parallel.ForEachAsync(ahkClasses, async (value, token) =>
{
    var (className, ahkClass) = value;
    var classDoc = new StringBuilder(
        $"""
          # `{className}`

          {ahkClass.Description.Indent(1)}
          """
    ).AppendLine();

    if (ahkClass.Extends != null)
    {
        classDoc.AppendLine($"\n#### Extends: {TypeDisplay(ahkClass.Extends)}");
    }

    if (ahkClass.Constructor != null && !string.IsNullOrWhiteSpace(ahkClass.Constructor.Description))
    {
        var header = $"## Constructor `{ahkClass.Constructor.Name}({string.Join(", ", ahkClass.Constructor.Parameters)})` {HeaderId("constructor")}";

        classDoc.AppendLine(MethodDocs(ahkClass.Constructor, header).Trim());
        classDoc.AppendLine();
    }

    var properties = ahkClass.Properties.Where(p => !p.Name.IsUpperCase()).ToList();
    if (properties.Count > 0)
    {
        classDoc.AppendLine("\n## Properties");
        foreach (var property in properties)
        {
            classDoc.AppendLine(
                $"""
                     * #### {(property.Static ? "**Static** " : string.Empty)}`{property.Name}` : {TypeDisplay(property.Type)} {HeaderId($"{(property.Static ? "static-" : string.Empty)}{property.Name}")}
                     {property.Description.Indent(1)}
                     """.Trim()
            );
        }
    }

    if (ahkClass.Methods.Count > 0)
    {
        classDoc.AppendLine("\n## Methods");
        foreach (var method in ahkClass.Methods)
        {
            var parameters = string.Join(", ", method.Parameters);
            var header = method.Static ? $"* ### **Static** `{method.Name}({parameters}) {HeaderId($"static-{method.Name}")}`"
                : $"* ### `{method.Name}({parameters}) {HeaderId(method.Name)}`";

            classDoc.AppendLine(MethodDocs(method, header));
            classDoc.AppendLine("\n______");
        }
    }

    var classFilePath = Path.Combine(config.OutputDir, $"{className.ToLower()}.md");
    var markdownContent = classDoc.ToString()
        .NormalizeLineEndings()
        .Trim('_', '-', ' ', '\t', '\n');

    await File.WriteAllTextAsync(classFilePath, markdownContent, token);
    Console.WriteLine($"Docs for {className} written to {classFilePath}");
});

return 0;

void ProcessAhkClass(JsonNode? classObj)
{
    if (classObj == null)
    {
        return;
    }

    var className = classObj["name"]!.ToString();
    if (ahkClasses.ContainsKey(className))
    {
        return;
    }

    var ahkClass = new AhkClass
    {
        Name = className,
        Extends = classObj["extends"]?.ToString()
    };

    Console.WriteLine($"Processing class {ahkClass.Name}...");

    // Get and parse the class's comment
    var classComment = classObj["comment"]?.ToString();
    if (!string.IsNullOrWhiteSpace(classComment))
    {
        var classJsDocComment = classComment.FormatAsJsDoc();
        var jsDoc = jsDocParser.Call(classJsDocComment, defaultJsDocOptions).AsArray()[0].AsObject();
        ahkClass.Description = jsDoc["description"].ToString().ParseDescription().NormalizeLineEndings();

        // Add any "see" tags to the description
        var seeTags = jsDoc["tags"]
            .AsArray()
            .Select(t => t.AsObject())
            .Where(t => t["tag"].ToString() == "see").ToArray();

        if (seeTags.Length > 0)
        {
            var seeBuilder = new StringBuilder("**See also**:\n");

            foreach (var seeTag in seeTags)
            {
                seeBuilder.AppendLine($"- {$"{{{seeTag["type"]}}} {seeTag["name"]} {seeTag["description"]}".Trim().ParseDescription().NormalizeLineEndings()}");
            }

            ahkClass.Description = $"""
                                    {ahkClass.Description}


                                    {seeBuilder}
                                    """.Trim().NormalizeLineEndings();
        }
    }

    // Process methods and properties
    var processedMethods = ProcessAhkMethods(classObj["methods"]?.AsArray());
    var constructor = processedMethods.FirstOrDefault(m => m.Name == "__New");

    ahkClass.Constructor = constructor;
    ahkClass.Methods = processedMethods.Except([constructor]).ToList();
    ahkClass.Properties = ProcessAhkProperties(classObj["properties"]?.AsArray());
    ahkClasses[className] = ahkClass;

    // Recursively process inner classes
    if (classObj["classes"]?.AsArray() is JsonArray innerClasses)
    {
        foreach (var innerClass in innerClasses)
        {
            ProcessAhkClass(innerClass);
        }
    }
}

List<AhkMethod> ProcessAhkMethods(JsonArray? methods)
{
    var ahkMethods = new List<AhkMethod>();
    if (methods is null or { Count: 0 })
    {
        return ahkMethods;
    }

    foreach (var methodJson in methods)
    {
        var method = new AhkMethod
        {
            Name = methodJson!["name"]!.ToString(),
            Static = methodJson["static"]?.GetValue<bool>() ?? false
        };

        // Exclude private methods (but include the constructor)
        var isPrivate = method.Name.StartsWith('_') && method.Name != "__New";
        Console.WriteLine($"\tProcessing method {method.Name}...");

        var parameters = methodJson["params"]?.AsArray() ?? [];
        foreach (var param in parameters)
        {
            var defaultVal = param!["defval"]?.ToString();
            var parameter = new AhkParameter
            {
                DefaultValue = defaultVal,
                IsOptional = defaultVal != null,
                Name = param["name"]!.ToString()
            };

            method.Parameters.Add(parameter);
        }

        var methodComment = methodJson["comment"]?.ToString();
        if (!string.IsNullOrWhiteSpace(methodComment))
        {
            var methodJsDocComment = methodComment.FormatAsJsDoc();

            var jsDoc = jsDocParser.Call(methodJsDocComment, defaultJsDocOptions).AsArray()[0].AsObject();
            method.Description = jsDoc["description"].ToString().ParseDescription().NormalizeLineEndings();

            var tags = jsDoc["tags"]
                .AsArray()
                .Select(t => t.AsObject())
                .ToArray();

            if (tags.Any(t => t["tag"].ToString() == "private"))
            {
                isPrivate = true;
            }

            // Extract more info for the method parameters 
            foreach (var paramTag in tags.Where(t => t["tag"].ToString() == "param"))
            {
                var param = method.Parameters.FirstOrDefault(p => p.Name == paramTag["name"].ToString());
                if (param == null)
                {
                    continue;
                }

                param.Type = string.IsNullOrWhiteSpace(paramTag["type"].ToString()) ? null : paramTag["type"].ToString();
                param.Description = paramTag["description"].ToString().ParseDescription().NormalizeLineEndings();
            }

            var returnTag = tags.FirstOrDefault(t => t["tag"].ToString() == "returns");
            if (returnTag != null)
            {
                method.Returns = new AhkValue
                {
                    Type = returnTag["type"].ToString(),
                    Description = $"{returnTag["name"]} {returnTag["description"]}".ParseDescription().NormalizeLineEndings()
                };
            }

            var throwsTags = tags.Where(t => t["tag"].ToString() == "throws");
            foreach (var throwsTag in throwsTags)
            {
                method.Throws.Add(new AhkValue
                {
                    Type = throwsTag["type"].ToString(),
                    Description = $"{throwsTag["name"]} {throwsTag["description"]}".ParseDescription().NormalizeLineEndings()
                });
            }

            var seeTags = tags.Where(t => t["tag"].ToString() == "see").ToArray();
            if (seeTags.Length > 0)
            {
                var seeBuilder = new StringBuilder("**See also**:\n");

                foreach (var seeTag in seeTags)
                {
                    seeBuilder.AppendLine($"- {{{seeTag["type"]}}} {seeTag["name"]} {seeTag["description"]}".Trim().ParseDescription().NormalizeLineEndings());
                }

                method.Description = $"""
                                      {method.Description}


                                      {seeBuilder}
                                      """.Trim().NormalizeLineEndings();
            }
        }

        if (!isPrivate)
        {
            ahkMethods.Add(method);
        }
    }

    return ahkMethods;
}

List<AhkProperty> ProcessAhkProperties(JsonArray? properties)
{
    var ahkProperties = new List<AhkProperty>();
    if (properties is null or { Count: 0 })
    {
        return ahkProperties;
    }

    foreach (var propertyJson in properties)
    {
        var property = new AhkProperty
        {
            Name = propertyJson!["name"]!.ToString(),
            Static = propertyJson["static"]?.GetValue<bool>() ?? false
        };

        // Exclude private properties
        var isPrivate = property.Name.StartsWith('_');
        Console.WriteLine($"\tProcessing property {property.Name}...");

        var parameters = propertyJson["params"]?.AsArray() ?? [];
        foreach (var param in parameters)
        {
            var defaultVal = param!["defval"]?.ToString();
            var parameter = new AhkParameter
            {
                DefaultValue = defaultVal,
                IsOptional = defaultVal != null,
                Name = param["name"]!.ToString()
            };

            property.Parameters.Add(parameter);
        }

        var propertyComment = propertyJson["comment"]?.ToString();
        if (!string.IsNullOrWhiteSpace(propertyComment))
        {
            var jsDoc = jsDocParser.Call(propertyComment.FormatAsJsDoc(), defaultJsDocOptions).AsArray()[0].AsObject();

            property.Description = jsDoc["description"].ToString().ParseDescription().NormalizeLineEndings();

            var tags = jsDoc["tags"]
                .AsArray()
                .Select(t => t.AsObject())
                .ToArray();

            if (tags.Any(t => t["tag"].ToString() == "private"))
            {
                isPrivate = true;
            }

            var typeTag = tags.FirstOrDefault(t => t["tag"].ToString() == "type");
            if (typeTag != null)
            {
                property.Type = typeTag["type"].ToString();
            }

            var paramTags = tags.Where(t => t["tag"].ToString() == "param");
            foreach (var paramTag in paramTags)
            {
                var param = property.Parameters.FirstOrDefault(p => p.Name == paramTag["name"].ToString());
                if (param == null)
                {
                    continue;
                }

                param.Type = string.IsNullOrWhiteSpace(paramTag["type"].ToString()) ? null : paramTag["type"].ToString();
                param.Description = paramTag["description"].ToString().ParseDescription().NormalizeLineEndings();
            }

            var seeTags = tags.Where(t => t["tag"].ToString() == "see").ToArray();
            if (seeTags.Length > 0)
            {
                var seeBuilder = new StringBuilder("**See also**:\n");

                foreach (var seeTag in seeTags)
                {
                    seeBuilder.AppendLine($"- {{{seeTag["type"]}}} {seeTag["name"]} {seeTag["description"]}".Trim().ParseDescription().NormalizeLineEndings());
                }

                property.Description = $"""
                                        {property.Description}

                                        {seeBuilder}
                                        """.Trim().NormalizeLineEndings();
            }
        }

        if (!isPrivate)
        {
            ahkProperties.Add(property);
        }
    }

    return ahkProperties;
}

string MethodDocs(AhkMethod method, string header)
{
    var methodBuilder = new StringBuilder(header).AppendLine();
    methodBuilder.AppendLine($"{method.Description.Indent(1)}\n");

    if (method.Parameters.Count > 0)
    {
        methodBuilder.AppendLine("**Parameters**:".Indent(1));
        foreach (var parameter in method.Parameters)
        {
            var optionalLabel = parameter.IsOptional ? "**Optional** " : string.Empty;
            var description = string.IsNullOrWhiteSpace(parameter.Description) ? string.Empty : $" - {parameter.Description.Indent(1).Trim()}"
                .Replace("(Optional) ", string.Empty);

            var paramDocs = $"- {optionalLabel}`{parameter.Name}` : {TypeDisplay(parameter.Type ?? "Any")}{description}";
            methodBuilder.AppendLine($"{paramDocs.Indent(1)}\n");
        }
    }

    if (method.Throws.Count > 0)
    {
        methodBuilder.AppendLine("**Throws**:".Indent(1));

        foreach (var throws in method.Throws)
        {
            methodBuilder.AppendLine($"- {TypeDisplay(throws.Type)} - {throws.Description.Indent(1).Trim()}".Indent(1));
        }

        methodBuilder.AppendLine();
    }

    if (method.Returns != null)
    {
        methodBuilder.AppendLine($"**Returns**: {TypeDisplay(method.Returns.Type)} - {method.Returns.Description.Indent(1).Trim()}".Indent(1));
    }

    return methodBuilder.ToString();
}

string HeaderId(string id)
{
    return config.IncludeHeaderIds ? $":id={id.ToLower()}" : string.Empty;
}

string TypeDisplay(string? typeName)
{
    typeName ??= "Any";
    return ahkClasses.ContainsKey(typeName) ? $"[`{typeName}`]({typeName.ToDocUri(true)})" : $"`{typeName}`";
}

static Function ImportJsDocParser(Engine jsEngine)
{
    var commentParserModule = jsEngine.Modules.Import("./comment-parser/es6/index.js");
    return commentParserModule.Get("parse").AsFunctionInstance();
}