const vscode = require('vscode');
const fs = require('fs');
const path = require('path');

exports.run = async function () {
    const directory = process.env.DV_EDITOR_EVIDENCE;
    const records = [];
    function record(event, details) {
        records.push({ event, at: new Date().toISOString(), ...details });
        fs.writeFileSync(path.join(directory, 'editor-session.json'), JSON.stringify(records, null, 2));
    }
    const file = path.join(directory, 'editing.dv');
    const uri = vscode.Uri.file(file);
    const waitFor = async predicate => {
        const deadline = Date.now() + 240000;
        while (Date.now() < deadline) {
            const diagnostics = vscode.languages.getDiagnostics(uri);
            if (predicate(diagnostics)) return diagnostics;
            await new Promise(resolve => setTimeout(resolve, 200));
        }
        throw new Error('Timed out waiting for compiler diagnostics');
    };
    try {
        const document = await vscode.workspace.openTextDocument(uri);
        await vscode.window.showTextDocument(document);
        const extension = vscode.extensions.getExtension('duelview.duelview-dv');
        await extension.activate();
        record('opened', { language: document.languageId, extension: extension.id });
        if (document.languageId !== 'duelview') throw new Error('DV language not registered');
        const edit = new vscode.WorkspaceEdit();
        edit.replace(uri, new vscode.Range(0, 0, document.lineCount, 0), 'fn belief() -> Acceptance:\n    return 1.2\n');
        await vscode.workspace.applyEdit(edit);
        const errors = await waitFor(items => items.some(item => item.source === 'DV compiler'));
        record('unsaved-error', { dirty: document.isDirty, diagnostics: errors.map(item => ({
            source: item.source, message: item.message, start: item.range.start, end: item.range.end
        })) });
        const correction = new vscode.WorkspaceEdit();
        correction.replace(uri, new vscode.Range(0, 0, document.lineCount, 0), 'fn belief() -> Acceptance:\n    return 0.6\n');
        await vscode.workspace.applyEdit(correction);
        await waitFor(items => items.length === 0);
        record('corrected-diagnostics-cleared', { version: document.version });
        await document.save();
    } catch (error) {
        record('error', { message: String(error), stack: error.stack });
        throw error;
    }
};
