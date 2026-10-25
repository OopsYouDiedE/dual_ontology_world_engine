"""Render the current work and evidence as a local HTML report; runs no checks."""
from __future__ import annotations

import html
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "artifacts/dv-approx-highlights"


def render() -> Path:
    OUT.mkdir(parents=True, exist_ok=True)
    stages = json.loads((ROOT / "godot_csharp/Contracts/highlight_stages.json").read_text(encoding="utf-8"))["stages"]
    status_path = OUT / "status.json"
    status = json.loads(status_path.read_text(encoding="utf-8")) if status_path.exists() else {}
    e = lambda value: html.escape(str(value), quote=True)
    def link(path: str, label: str) -> str:
        return f'<a href="../../{e(path)}">{e(label)}</a>'
    cards = []
    for stage in stages:
        lines = ''.join(f'<p><b>{e(line["speaker"])}</b>　{e(line["text"])}</p>' for line in stage["lines"])
        cards.append(f'<article id="{e(stage["id"])}"><small>{e(stage["location"])}</small><h2>{e(stage["title"])}</h2><p class="condition">{e(stage["condition"])}</p>{lines}</article>')
    rows = ''.join(f'<tr><td>{e(row["area"])}</td><td>{e(row["change"])}</td><td>{e(row["result"])}</td></tr>' for row in status.get("changes", []))
    observations = ''.join(f'<li>{e(item)}</li>' for item in status.get("observations", []))
    media = []
    for item in status.get("images", []):
        media.append(f'<figure><a href="../../{e(item["path"])}"><img loading="lazy" src="../../{e(item["path"])}" alt="{e(item["caption"])}"></a><figcaption>{e(item["caption"])}</figcaption></figure>')
    for item in status.get("runtime_images", []):
        relative = item["path"]
        caption = item["caption"]
        media.append(f'<figure><a href="../../{e(relative)}"><img loading="lazy" src="../../{e(relative)}" alt="{e(caption)}"></a><figcaption>{e(caption)}</figcaption></figure>')
    comparisons = []
    concepts = []
    for item in status.get("concept_images", []):
        concepts.append(f'<figure><a href="../../{e(item["path"])}"><img loading="lazy" src="../../{e(item["path"])}" alt="{e(item["caption"])}"></a><figcaption>{e(item["caption"])}</figcaption></figure>')
    for item in status.get("scene_comparisons", []):
        pair = ''.join(f'<figure><img loading="lazy" src="../../{e(item[key])}" alt="{e(label)}"><figcaption>{e(label)}</figcaption></figure>' for key, label in (("reference", "效果原稿"), ("runtime", "实际场景")))
        comparisons.append(f'<article><h2>{e(item["title"])}</h2><div class="gallery">{pair}</div><p>{e(item["result"])}</p></article>')
    commits = ''.join(f'<li><a href="{e(item["url"])}">{e(item["sha"])}</a>　{e(item["summary"])}</li>' for item in status.get("pushed_commits", []))
    sources = ''.join(f'<li>{link(item["path"], item["label"])}</li>' for item in status.get("sources", []))
    navigation = ''.join(f'<a href="#{e(stage["id"])}">{e(stage["title"])}</a>' for stage in stages)
    document = f'''<!doctype html>
<html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>安眠者有福 · DV近似模拟与六幕重绘</title>
<style>
:root{{color-scheme:light;--ink:#221c15;--paper:#eae0c4;--green:#24483e;--red:#8a3324}}*{{box-sizing:border-box}}
body{{margin:0;background:#f5f1e6;color:var(--ink);font:16px/1.85 'Microsoft YaHei',sans-serif}}header{{background:var(--green);color:#f8f0d9;padding:48px max(6vw,24px)}}header small{{letter-spacing:.16em}}h1{{font:600 clamp(28px,4vw,46px)/1.4 Georgia,serif;margin:12px 0}}main{{max-width:1160px;margin:auto;padding:24px}}nav{{display:flex;flex-wrap:wrap;gap:8px;margin:20px 0}}nav a{{border:1px solid #b4ac97;padding:6px 12px;text-decoration:none}}a{{color:var(--green)}}header a{{color:#f8f0d9}}section,article{{background:#fffdf5;border:1px solid #d6cbb1;padding:24px;margin:20px 0;border-radius:4px}}h2{{font-size:23px;margin:6px 0 16px}}.condition{{background:#ede8d9;border-left:3px solid #7e6f58;padding:10px 14px}}.meta{{font-size:14px;color:#5a4c3b}}table{{width:100%;border-collapse:collapse}}th,td{{border-bottom:1px solid #ddd3bc;text-align:left;padding:12px;vertical-align:top}}th{{background:#eae0c4}}figure{{margin:16px 0}}img{{max-width:100%;height:auto;background:repeating-conic-gradient(#ddd 0% 25%,#fff 0% 50%) 0/24px 24px;image-rendering:pixelated}}figcaption{{font-size:14px;color:#5a4c3b}}.gallery{{display:grid;grid-template-columns:repeat(auto-fit,minmax(280px,1fr));gap:20px}}article small{{color:#5a4c3b}}footer{{padding:28px;text-align:center;color:#5a4c3b}}@media(max-width:650px){{section,article{{padding:16px}}table{{font-size:14px}}th,td{{padding:6px}}}}
</style>
<header><small>BLESSED ARE THOSE WHO SLEEP · 2026-10-24</small><h1>DV近似模拟与六幕重绘</h1><p>{e(status.get("summary", "当前正在实施。源码、素材与运行观察随本轮工作更新。"))}</p></header>
<main><p class="meta">基线：{e(status.get("baseline", "origin/main d9c8a91e"))}　·　汇报状态：{e(status.get("state", "实施中"))}</p>
<section><h2>六幕场景设计图</h2><p>按设计正文绘制的构图目标，尚未接入游戏。先核对人物正在做什么、行动对象与事件结果。</p><div class="gallery">{''.join(concepts)}</div></section>
<nav><a href="#changes">修改与结果</a><a href="#assets">素材与现场</a><a href="#observations">实际观察</a>{navigation}</nav>
<section id="changes"><h2>修改与结果</h2><ul>{commits}</ul><table><thead><tr><th>范围</th><th>修改</th><th>当前结果</th></tr></thead><tbody>{rows}</tbody></table></section>
<section id="assets"><h2>分类素材与现场画面</h2><p>运行地形按32像素格组织，自动地形、独立物件和人物分别发布。图集与场景的实际引用、拼接和遮挡分别记录。</p><div class="gallery">{''.join(media) or '<p>本轮图像正在生成与接线，现场画面待采集。</p>'}</div></section>
{''.join(comparisons)}<section id="observations"><h2>实际观察与修正</h2><ul>{observations or '<li>尚未进行本轮实际交互与画面查看。</li>'}</ul><p class="meta">报告记录实际执行、可见效果和源码修正。构建诊断、舞台节选、正式会话分别注明范围。</p></section>
<section><h2>六段高光节选</h2><p>以下为本轮依据设计新写的舞台文本。每幕注明成立条件，演出范围限定于在场人员。源质具体液面保持未审定状态，棋局按十九路实际落子。</p>{link('godot_csharp/Contracts/highlight_stages.json', '运行内容源：highlight_stages.json')}</section>
{''.join(cards)}<section><h2>源码与素材入口</h2><ul>{sources}</ul></section></main><footer>本地工作汇报 · 不执行游戏动作或自动验收</footer></html>'''
    target = OUT / "index.html"
    target.write_text(document, encoding="utf-8")
    return target


if __name__ == "__main__":
    print(render())
