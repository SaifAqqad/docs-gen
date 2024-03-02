using System.Text.Json;
using Jint;
using Jint.Native.Function;

var parser = InitJsParser();


static Function InitJsParser()
{
    var jsEngine = new Engine(options =>
    {
        options.EnableModules(@$"{Environment.CurrentDirectory}\js");
    });

    var commentParserModule = jsEngine.Modules.Import("./comment-parser/es6/index.js");
    return commentParserModule.Get("parse").AsFunctionInstance();
}