const fs = require('fs');
const path = require('path');
fs.mkdirSync(path.join(__dirname, 'server'), { recursive: true });
fs.copyFileSync(path.join(__dirname, '../../tools/dv_lsp.py'), path.join(__dirname, 'server/dv_lsp.py'));
