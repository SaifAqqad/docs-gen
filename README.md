### docs-gen

Generates Markdown docs for an ahk file using the JSON file generated
by [thqby's vscode extension](https://github.com/thqby/vscode-autohotkey2-lsp)

![vscode-autohotkey2-lsp](https://github.com/SaifAqqad/docs-gen/assets/47293197/1cdcd248-98d1-4af1-8bb4-753ad3ad5fed)

The script relies on [Jint](https://github.com/sebastienros/jint) to run and use the js library [comment-parser](https://github.com/syavorsky/comment-parser) which parses the jsdoc-style comments, and then generates Markdown docs for the AHK classes in the input JSON.
