### docs-gen

Generates Markdown docs for an ahk file using the JSON file generated
by [thqby's vscode extension](https://github.com/thqby/vscode-autohotkey2-lsp)

![vscode-autohotkey2-lsp](https://gist.github.com/assets/47293197/8356b07e-ea04-4486-90f6-fb4142844813)

The script relies on [Jint](https://github.com/sebastienros/jint) to run and use the js library [comment-parser](https://github.com/syavorsky/comment-parser) which parses the jsdoc-style comments, and then generates Markdown docs for the AHK classes in the input JSON.