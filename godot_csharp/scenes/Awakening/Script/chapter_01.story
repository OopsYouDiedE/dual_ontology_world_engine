章节：awakening

# 《安眠者有福》全剧作者源的入口。两章共用一份生态，第二章写在 `续篇 chapter2` 作用域里。
# 片段按下面的顺序引入，顺序即声明顺序：人物、地点须先声明后使用（同 Python 的顺序语义）。
# 分段引入与把全部片段写进同一文件等价；片段不能声明章节，生成物由 tools/compile_story.py 产出，不得手改。

引入：story/剧情时长.story

# ---- 全章共享生态：词汇、地点、十六人画像与晨间日常 ----
引入：story/生态/词汇.story
引入：story/生态/地点.story
引入：story/生态/物件名.story
引入：story/生态/人物/*.story
引入：story/生态/方法库.story
引入：story/生态/晨间日常.story

# ---- 第一章《门的这一侧》：证物、布局、推演、文案、流程与场面 ----
引入：story/第一章/证物.story
引入：story/第一章/热点与布局.story
引入：story/第一章/推演变量与陈述.story
引入：story/第一章/可习得命题.story
引入：story/第一章/提示与站位.story
引入：story/第一章/文案与守则.story
引入：story/第一章/声路与真相.story
引入：story/第一章/阶段与流程.story
引入：story/第一章/话题.story
引入：story/第一章/表达.story
引入：story/第一章/场面/*.story
引入：story/第一章/模拟侦探.story
引入：story/第一章/寝区案件.story
引入：story/第一章/出区日常.story

# ---- 续篇（第二章）：始业与后续案件 ----
引入：story/续篇/*.story

# ---- 运行：此前各句只登记内容。运行声明一次模拟的时长（自剧情时长的起始分钟起算）；
# 外部测试可用 --duration 参数代替或覆盖。写“到结局”即运行到剧情自然结束。
运行
    时长：到结局
