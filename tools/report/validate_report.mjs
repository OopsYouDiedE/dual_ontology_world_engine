import { spawn } from 'node:child_process';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const output = path.join(root, 'artifacts/code-analysis');
const report = JSON.parse(await readFile(path.join(output, 'analysis.json'), 'utf8'));
const chrome = process.env.REPORT_CHROME ?? 'C:/Program Files/Google/Chrome/Application/chrome.exe';
await mkdir(output, { recursive: true });
const browser = spawn(chrome, ['--headless=new', '--disable-gpu', '--no-first-run', '--no-default-browser-check', '--disable-background-networking', '--remote-debugging-port=0', `--user-data-dir=${path.join(output, 'browser-profile')}`, 'about:blank'], { windowsHide: true, stdio: ['ignore', 'ignore', 'pipe'] });
const socketUrl = await new Promise((resolve, reject) => {
    let messages = '';
    const deadline = setTimeout(() => reject(new Error('Chrome startup deadline exceeded')), 20000);
    browser.once('error', reject);
    browser.stderr.on('data', chunk => {
        messages += chunk.toString();
        const match = messages.match(/DevTools listening on (ws:\/\/[^\s]+)/);
        if (match) { clearTimeout(deadline); resolve(match[1]); }
    });
});
const socket = new WebSocket(socketUrl);
await new Promise((resolve, reject) => { socket.onopen = resolve; socket.onerror = reject; });
const pending = new Map();
const errors = [];
let sequence = 0;
socket.onmessage = event => {
    const message = JSON.parse(event.data);
    if (message.id && pending.has(message.id)) {
        const callback = pending.get(message.id);
        pending.delete(message.id);
        if (message.error) callback.reject(new Error(JSON.stringify(message.error)));
        else callback.resolve(message.result);
    }
    if (message.method === 'Runtime.exceptionThrown') errors.push(message.params.exceptionDetails);
};
function command(method, params = {}, sessionId) {
    const id = ++sequence;
    return new Promise((resolve, reject) => {
        const timeout = setTimeout(() => { pending.delete(id); reject(new Error(`${method} deadline exceeded`)); }, 20000);
        pending.set(id, { resolve: value => { clearTimeout(timeout); resolve(value); }, reject: error => { clearTimeout(timeout); reject(error); } });
        socket.send(JSON.stringify({ id, method, params, ...(sessionId ? { sessionId } : {}) }));
    });
}
const checks = [];
function check(name, passed, details = '') {
    checks.push({ name, passed: Boolean(passed), details });
    if (!passed) throw new Error(`${name}: ${details}`);
}
let session;
try {
    check('Git 跟踪代码覆盖', report.coverage.missingTracked.length === 0, `${report.coverage.trackedCode} tracked`);
    const functionIds = report.functions.map(item => item.id);
    check('函数标识唯一', new Set(functionIds).size === functionIds.length, String(functionIds.length));
    check('函数行号与正文', report.functions.every(item => item.line > 0 && item.end >= item.line && item.source.trim().length > 0));
    check('16 个模块覆盖全部源码和资源', report.partitions.modules.length === 16 && report.partitions.validation.unassigned.length === 0 && report.files.every(file => report.partitions.modules.some(module => module.id === file.partition)) && report.resources.every(file => report.partitions.modules.some(module => module.id === file.partition)));
    check('模块函数归属无漏计', report.partitions.modules.reduce((total, module) => total + module.functionCount, 0) === report.functions.length);
    const testIds = report.partitions.modules.flatMap(module => module.tests.map(test => test.id));
    check('32 个验收用例标识唯一且不冒充已执行', testIds.length === 32 && new Set(testIds).size === 32 && report.partitions.validation.gameTestsRun === false);
    const functional = report.functional;
    const secondaryIds = functional.systems.flatMap(system => system.subsystems.map(subsystem => subsystem.id));
    check('五 UI 八场景及二级验收定义', functional.systems.filter(system => system.group === 'UI').length === 5 && functional.systems.filter(system => system.group === '场景').length === 8 && new Set(secondaryIds).size === secondaryIds.length && functional.systems.every(system => system.subsystems.every(subsystem => subsystem.accept)) && functional.validation.gameTestsRun === false, `${secondaryIds.length} secondary`);
    check('共享契约、内置能力及官方来源', functional.shared.length === 4 && functional.engine.length === 12 && functional.sources.length === 14 && functional.sources.every(source => source.url.startsWith('https://docs.godotengine.org/en/stable/')));
    const coreDesign = report.coreDesign;
    const coreMap = new Map(coreDesign.systems.map(system => [system.id, system]));
    check('Core 基础与特色设计覆盖', coreMap.size === 18 && coreDesign.validation.base === 11 && coreDesign.validation.extensions === 7 && coreDesign.validation.secondary === 54 && coreDesign.infrastructure.length === 4 && coreDesign.acceptance.length === 7);
    check('Core 预设依赖闭包与基础无反向依赖', coreDesign.profiles.length === 4 && coreDesign.profiles.find(profile => profile.id === 'life').enabled.length === 0 && coreDesign.profiles.every(profile => profile.enabled.every(id => coreMap.get(id).dependencies.filter(dependency => coreMap.get(dependency).group === '特色扩展').every(dependency => profile.enabled.includes(dependency)))) && coreDesign.systems.filter(system => system.group === '生活基础').every(system => system.dependencies.every(dependency => coreMap.get(dependency).group === '生活基础')));
    check('Core 运行开关与验收状态如实标记', coreDesign.validation.switchesImplemented === false && coreDesign.validation.gameTestsRun === false);
    const { targetId } = await command('Target.createTarget', { url: 'about:blank' });
    session = (await command('Target.attachToTarget', { targetId, flatten: true })).sessionId;
    await command('Runtime.enable', {}, session);
    await command('Page.enable', {}, session);
    await command('Emulation.setDeviceMetricsOverride', { width: 1440, height: 1100, deviceScaleFactor: 1, mobile: false }, session);
    await command('Page.navigate', { url: pathToFileURL(path.join(output, 'index.html')).href }, session);
    async function evaluate(expression) {
        const result = await command('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true }, session);
        if (result.exceptionDetails) throw new Error(JSON.stringify(result.exceptionDetails));
        return result.result.value;
    }
    await evaluate(`new Promise((resolve,reject)=>{const deadline=Date.now()+15000;const timer=setInterval(()=>{if(document.documentElement.dataset.reportReady==='true'){clearInterval(timer);resolve(true);}else if(Date.now()>deadline){clearInterval(timer);reject(new Error('Report ready deadline'));}},50);})`);
    check('HTML 初始化', await evaluate(`document.documentElement.dataset.reportReady==='true'`));
    check('九个报告章节', await evaluate(`document.querySelectorAll('section[role="tabpanel"]').length===9`));
    check('无外部资源请求', await evaluate(`performance.getEntriesByType('resource').every(entry=>!/^https?:/.test(entry.name))`));
    await evaluate(`document.querySelector('[data-tab="systems"]').click();document.querySelector('[data-system="SC05"]').click();`);
    check('角色验收系统与证据边界', await evaluate(`document.querySelectorAll('[data-system]').length===13 && document.getElementById('systemDetail').textContent.includes('正式新局') && document.getElementById('systemDetail').textContent.includes('未覆盖') && document.getElementById('systemDetail').textContent.includes('SC05.6')`));
    await evaluate(`document.querySelector('[data-system="UI05"]').click()`);
    check('书 UI 二级验收及官方链接', await evaluate(`document.getElementById('systemDetail').textContent.includes('UI05.4') && document.querySelector('#systemDetail a[href*="class_subviewport"]')!==null`));
    await evaluate(`document.querySelector('#systemDetail [data-kind="file"]').click()`);
    check('功能系统到完整源码', await evaluate(`document.getElementById('detail').open && document.getElementById('detailBody').textContent.includes('完整源码')`));
    await evaluate(`document.getElementById('closeDetail').click()`);
    await evaluate(`document.querySelector('[data-tab="core"]').click();document.querySelector('[data-profile="life"]').click();document.querySelector('#coreChoices [data-core="X05"]').click();`);
    check('生活模式与策略关闭回退', await evaluate(`document.querySelectorAll('#coreChoices [data-core]').length===18 && document.getElementById('coreProfileState').textContent.includes('全部关闭') && document.getElementById('coreDetail').textContent.includes('关闭特色扩展') && document.getElementById('coreDetail').textContent.includes('单步生活效用') && document.getElementById('coreDetail').textContent.includes('X05.3')`));
    await evaluate(`document.querySelector('[data-profile="full"]').click()`);
    check('完整模式启用及依赖跳转', await evaluate(`document.getElementById('coreProfileState').textContent.includes('X07') && document.querySelector('#coreDetail .pill').textContent.includes('启用')`));
    await evaluate(`document.querySelector('#coreDetail [data-core="X04"]').click();document.querySelector('#coreDetail [data-kind="file"]').click()`);
    check('Core 依赖与源码详情', await evaluate(`document.getElementById('detail').open && document.getElementById('detailTitle').textContent.includes('NaturalDeduction.cs')`));
    await evaluate(`document.getElementById('closeDetail').click();document.querySelector('[data-profile="life"]').click();document.querySelector('#coreChoices [data-core="B09"]').click()`);
    check('生活类型补全未冒充现有经济实现', await evaluate(`document.getElementById('coreDetail').textContent.includes('没有据此成立')`));
    await evaluate(`document.querySelector('[data-tab="functions"]').click();document.getElementById('functionSearch').value='EvaluateSuggestion';document.getElementById('functionSearch').dispatchEvent(new Event('input'));`);
    check('函数搜索', await evaluate(`!document.getElementById('functions').hidden && document.getElementById('functionTable').textContent.includes('EvaluateSuggestion')`));
    await evaluate(`[...document.querySelectorAll('#functionTable [data-kind="function"]')].find(button=>/(^|\\.)EvaluateSuggestion$/.test(button.textContent)).click()`);
    check('函数详情与机制说明', await evaluate(`document.getElementById('detail').open && document.getElementById('detailBody').textContent.includes('目标本人记忆') && document.getElementById('detailBody').textContent.includes('完整函数正文')`));
    await evaluate(`document.querySelector('#detailBody [data-kind="file"]').click()`);
    check('函数到源码跳转', await evaluate(`document.getElementById('detailBody').textContent.includes('完整源码') && document.getElementById('detailTitle').textContent.includes('WorldRules.cs')`));
    await evaluate(`document.getElementById('closeDetail').click();document.querySelector('[data-tab="architecture"]').click();document.querySelector('[data-project="core/simulation/DuelView.Simulation.csproj"]').dispatchEvent(new MouseEvent('click',{bubbles:true}));`);
    check('依赖图节点交互', await evaluate(`document.getElementById('projectDetail').textContent.includes('core/autonomy') && document.querySelectorAll('.edge.active').length===6`));
    await evaluate(`document.querySelector('[data-tab="connections"]').click();document.getElementById('connectionType').value='signal';document.getElementById('connectionType').dispatchEvent(new Event('input'));document.getElementById('connectionSearch').value='TraceClosed';document.getElementById('connectionSearch').dispatchEvent(new Event('input'));`);
    check('信号证据搜索', await evaluate(`document.getElementById('connectionTable').textContent.includes('TraceClosed')`));
    await evaluate(`document.querySelector('[data-tab="files"]').click();document.getElementById('fileSearch').value='absent-'+Date.now();document.getElementById('fileSearch').dispatchEvent(new Event('input'));`);
    check('空搜索状态', await evaluate(`document.getElementById('fileTable').textContent.includes('没有匹配结果')`));
    await evaluate(`document.querySelector('[data-tab="partitions"]').click();document.querySelector('[data-partition="M07"]').click();`);
    check('模块切换与测试契约', await evaluate(`document.getElementById('partitionDetail').textContent.includes('M07-T1') && document.getElementById('partitionDetail').textContent.includes('HostCompleted') && document.querySelectorAll('.partitionDiagram rect').length===9`));
    await evaluate(`document.querySelector('[data-partition="M06"]').click();[...document.querySelectorAll('#partitionDetail [data-kind="file"]')].find(button=>button.textContent.endsWith('AutonomyDriveSolver.cs')).click();`);
    check('模块归属到源码详情', await evaluate(`document.getElementById('detail').open && document.getElementById('detailTitle').textContent.endsWith('AutonomyDriveSolver.cs')`));
    await evaluate(`document.getElementById('closeDetail').click()`);
    await command('Emulation.setDeviceMetricsOverride', { width: 390, height: 844, deviceScaleFactor: 1, mobile: true }, session);
    for (const identifier of ['core', 'systems', 'overview', 'architecture', 'partitions', 'files', 'functions', 'connections', 'findings']) {
        await evaluate(`document.querySelector('[data-tab="${identifier}"]').click()`);
        check(`390px 无页面横溢：${identifier}`, await evaluate('document.documentElement.scrollWidth<=window.innerWidth+1'));
    }
    await command('Emulation.setDeviceMetricsOverride', { width: 1440, height: 1100, deviceScaleFactor: 1, mobile: false }, session);
    await evaluate(`document.querySelector('[data-tab="overview"]').click();window.scrollTo(0,0);`);
    const screenshot = await command('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false }, session);
    await writeFile(path.join(output, 'preview.png'), Buffer.from(screenshot.data, 'base64'));
    await evaluate(`document.querySelector('[data-tab="partitions"]').click();document.querySelector('[data-partition="M09"]').click();window.scrollTo(0,document.getElementById('partitions').offsetTop-20);`);
    const moduleScreenshot = await command('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false }, session);
    await writeFile(path.join(output, 'module-preview.png'), Buffer.from(moduleScreenshot.data, 'base64'));
    await evaluate(`document.querySelector('[data-tab="systems"]').click();document.querySelector('[data-system="UI05"]').click();window.scrollTo(0,document.getElementById('systems').offsetTop-20);`);
    const systemScreenshot = await command('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false }, session);
    await writeFile(path.join(output, 'system-preview.png'), Buffer.from(systemScreenshot.data, 'base64'));
    await command('Emulation.setDeviceMetricsOverride', { width: 1440, height: 1400, deviceScaleFactor: 1, mobile: false }, session);
    await evaluate(`document.querySelector('[data-tab="core"]').click();document.querySelector('[data-profile="life"]').click();document.querySelector('#coreChoices [data-core="X05"]').click();window.scrollTo(0,document.getElementById('coreProfiles').offsetTop-50);`);
    const coreScreenshot = await command('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false }, session);
    await writeFile(path.join(output, 'core-preview.png'), Buffer.from(coreScreenshot.data, 'base64'));
    check('浏览器脚本异常', errors.length === 0, JSON.stringify(errors));
    await writeFile(path.join(output, 'validation.json'), JSON.stringify({ time: new Date().toISOString(), checks, errors }, null, 2));
    console.log(JSON.stringify({ passed: checks.length, failed: 0, preview: path.join(output, 'preview.png') }));
} catch (error) {
    await writeFile(path.join(output, 'validation.json'), JSON.stringify({ time: new Date().toISOString(), checks, errors, failure: String(error) }, null, 2));
    console.error(error);
    process.exitCode = 1;
} finally {
    await command('Browser.close').catch(() => {});
    socket.close();
    if (browser.exitCode === null) browser.kill();
}
