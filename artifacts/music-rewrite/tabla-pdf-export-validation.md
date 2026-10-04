# Evening 最终 PDF 重导验证

本轮独立上下文只验证当前 MSCZ 的 PDF 重导，未读取设计文件或旧报告。

- 输入：music/Blessed Sleepers - Evening.mscz。
- 输出：music/Blessed Sleepers - Evening.pdf。
- 导出程序：C:/Program Files/MuseScore 4/bin/MuseScore4.exe；PDF 元数据确认 MuseScore Studio 4.7.5。
- 本轮仅启动一个隐藏 MuseScore 导出进程（PID 41204），参数为 -o 输出PDF 输入MSCZ；退出码 0。
- 开始 UTC：10/04/2026 17:37:08；完成 UTC：10/04/2026 17:37:09。
- PDF 修改时间 UTC：10/04/2026 16:37:08 → 10/04/2026 17:37:09。
- PDF SHA256：39CEFCB4BDAD7E91C2B19E6D7C339449707E26F5E7C2CDA59AC23CFF1210C84B → F4C47F34ED7BCEE39DB9270FB23E1856FBFC67CB2CBF8E3F77B21C7876FC9E1D。PDF 确实已重导，最终大小 64030 字节。
- MSCZ 导出前后 SHA256 相同：DF990D57FBFD27662D9FF2E0340F0910A4D34BD561BE771886F6358ED9CDE126。修改时间同为 10/04/2026 17:28:49，源文件及用户编辑已保留。
- Poppler pdfinfo 确认 A4、2页；pdftoppm 以140 DPI渲染2页，退出码0。
- 已逐页目视检查本轮新导出的两张 PNG，另检查双页 contact：标题、中文分段标签、四声部、Tablas 打击乐谱线、符号及终止线清晰，无缺字、裁边、黑块或相互遮挡。第一页面分段标签与速度标记间距较紧，但均完整可读。
- 仅验证 PDF 产物；未验证音频听感、音色或作曲内容，未更改 MSCZ、MusicXML、MP3、其他曲目、源码或用户程序。

证据目录：C:\Users\iii\Desktop\dual_ontology_world_engine\tmp\tabla-soundfont\pdf-final

包含 before.json、export-result.json、export.stdout.log、export.stderr.log、pdfinfo.log、render.stderr.log、render-exit-code.txt、page-1.png、page-2.png 与 contact.png。

Contact 绝对路径：C:/Users/iii/Desktop/dual_ontology_world_engine/tmp/tabla-soundfont/pdf-final/contact.png
