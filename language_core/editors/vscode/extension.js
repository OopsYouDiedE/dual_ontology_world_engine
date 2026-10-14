const vscode = require('vscode');
const { LanguageClient } = require('vscode-languageclient/node');

let client;
let restarting = Promise.resolve();

async function start(context, output) {
    if (client) await client.stop();
    const config = vscode.workspace.getConfiguration('dv');
    const env = { ...process.env, PYTHONUTF8: '1' };
    if (config.get('checkerPath')) env.DV_CHECKER = config.get('checkerPath');
    client = new LanguageClient('duelview', 'DV compiler', {
        command: config.get('pythonPath', 'python'),
        args: [context.asAbsolutePath('server/dv_lsp.py')],
        options: { env }
    }, {
        documentSelector: [{ scheme: 'file', language: 'duelview' }],
        outputChannel: output
    });
    await client.start();
}

function activate(context) {
    const output = vscode.window.createOutputChannel('DV compiler');
    context.subscriptions.push(output);
    const restart = () => {
        restarting = restarting.then(() => start(context, output)).catch(error => {
            output.appendLine(String(error));
            vscode.window.showErrorMessage(`DV checker could not start: ${error.message}`);
        });
        return restarting;
    };
    context.subscriptions.push(vscode.commands.registerCommand('dv.restartChecker', restart));
    context.subscriptions.push(vscode.workspace.onDidChangeConfiguration(event => {
        if (event.affectsConfiguration('dv')) restart();
    }));
    return restart();
}

async function deactivate() {
    await restarting;
    if (client) await client.stop();
}

module.exports = { activate, deactivate };
