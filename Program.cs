using docs_gen;
using Jint;
using Jint.Native;
using Jint.Native.Function;
using System.Text.Json.Nodes;

var jsEngine = new Engine(options =>
{
    options.EnableModules(@$"{Environment.CurrentDirectory}\js");
});

var jsDocParser = ImportJsDocParser(jsEngine);
var defaultJsDocOptions = JsValue.FromObject(jsEngine, new
{
    spacing = "preserve" // Keep spaces/newlines in the comments
});

Console.Write("Enter the path to the AHK JSON file: ");
var ahkJsonFilePath = Console.ReadLine();

if (!File.Exists(ahkJsonFilePath))
{
    Console.WriteLine("File not found");
    return -1;
}

var ahkJson = JsonNode.Parse(File.ReadAllText(ahkJsonFilePath))!.AsObject();
var ahkClasses = new Dictionary<string, AhkClass>();

// Go through each file in the AHK JSON
foreach (var (fileName, content) in ahkJson)
{
    Console.WriteLine($"Processing {fileName}...");

    // Go through each class in the file
    if (content?["classes"]?.AsArray() is JsonArray classes)
    {
        foreach (var classObj in classes)
        {
            ProcessAhkClass(classObj);
        }
    }

    // TODO: maybe process functions and variables?
}

// TODO: Generate markdown docs for each class and output to files
return 0;

void ProcessAhkClass(JsonNode? classObj)
{
    if (classObj == null)
        return;

    var className = classObj["name"]!.ToString();
    if (ahkClasses.ContainsKey(className))
        return;

    var ahkClass = new AhkClass { Name = className };
    Console.WriteLine($"Processing class {ahkClass.Name}...");

    // Get and parse the class's comment
    var classComment = classObj["comment"]?.ToString();
    if (!string.IsNullOrWhiteSpace(classComment))
    {
        var classJsDocComment = classComment.FormatAsJsDoc();
        var jsDoc = jsDocParser.Call(classJsDocComment, defaultJsDocOptions).AsArray()[0].AsObject();
        ahkClass.Description = jsDoc["description"].ToString().AsDescription();
    }

    // Process methods and properties
    ahkClass.Methods = ProcessAhkMethods(classObj["methods"]?.AsArray());
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
            method.Description = jsDoc["description"].ToString().AsDescription();

            var tags = jsDoc["tags"]
                .AsArray()
                .Select(t => t.AsObject())
                .ToArray();

            // Extract more info for the method parameters 
            foreach (var paramTag in tags.Where(t => t["tag"].ToString() == "param"))
            {
                var param = method.Parameters.FirstOrDefault(p => p.Name == paramTag["name"].ToString());
                if (param == null)
                {
                    continue;
                }

                param.Type = paramTag["type"].ToString();
                param.Description = paramTag["description"].ToString().AsDescription();
            }

            var returnTag = tags.FirstOrDefault(t => t["tag"].ToString() == "returns");
            if (returnTag != null)
            {
                method.Returns = new AhkValue
                {
                    Type = returnTag["type"].ToString(),
                    Description = $"{returnTag["name"]} {returnTag["description"]}".Trim('-').AsDescription()
                };
            }
            
            var throwsTag = tags.FirstOrDefault(t => t["tag"].ToString() == "throws");
            if (throwsTag != null)
            {
                method.Throws = new AhkValue
                {
                    Type = throwsTag["type"].ToString(),
                    Description = $"{throwsTag["name"]} {throwsTag["description"]}".Trim('-').AsDescription()
                };
            }
            
            // TODO: process @see tags
        }

        ahkMethods.Add(method);
    }

    return ahkMethods;
}

static List<AhkProperty> ProcessAhkProperties(JsonArray? properties)
{
    throw new NotImplementedException();
}

static Function ImportJsDocParser(Engine jsEngine)
{
    var commentParserModule = jsEngine.Modules.Import("./comment-parser/es6/index.js");
    return commentParserModule.Get("parse").AsFunctionInstance();
}