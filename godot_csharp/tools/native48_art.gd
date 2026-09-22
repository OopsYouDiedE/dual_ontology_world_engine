extends RefCounted
## Raster drawing directly at the final pixel coordinates. No resampling.
const ROOT := "res://ShareAsset/Victorian/source/"
const OUT := "res://ShareAsset/Images/native48/"
var canvas: Image
var objects: Dictionary = {}
var palette := {"ink": Color("29251f"), "wood": Color("59402c"), "brass": Color("a38b54"), "light": Color("c6b17a"), "stone": Color("817b6c")}

func build() -> Dictionary:
	canvas=Image.create(960,768,false,Image.FORMAT_RGBA8)
	canvas.fill(Color.TRANSPARENT)
	var a2:=Image.load_from_file(ROOT+"Victorian_Inside_A2.png")
	var a4:=Image.load_from_file(ROOT+"Victorian_Inside_A4.png")
	var b:=Image.load_from_file(ROOT+"Victorian_Inside_B.png")
	# The original 48px stone sample remains one sample pixel per output pixel.
	for variant in 3:
		var tile:=a2.get_region(Rect2i(336,480,48,48))
		for y in 48:
			for x in 48:
				var value:=tile.get_pixel(x,y).get_luminance()
				var col:=Color(0.32+value*0.23,0.30+value*0.22,0.265+value*0.20)
				if x==47 or y==47: col=Color("5c574d")
				if x==0 or y==0: col=col.lightened(0.055)
				tile.set_pixel(x,y,col)
		if variant==1:
			poly(tile,[Vector2i(0,0),Vector2i(5,0),Vector2i(0,5)],Color("514e46"))
			poly(tile,[Vector2i(47,47),Vector2i(42,47),Vector2i(47,42)],Color("514e46"))
		if variant==2:
			line(tile,Vector2i(0,8),Vector2i(47,8),Color("9e8a60"))
			line(tile,Vector2i(0,10),Vector2i(47,10),Color("685c43"))
		put("floor_%d"%variant,tile,Vector2i(variant,4))
	put("clock",clock(),Vector2i(0,0))
	put("portal",portal(a4),Vector2i(4,0))
	for i in 5:
		put("flag_%d"%i,flag(i),Vector2i(7+i*2,0))
	put("table",table(b),Vector2i(3,4))
	put("counter",counter(),Vector2i(6,4))
	put("fire",fire(),Vector2i(9,4))
	put("seal",seal(),Vector2i(10,4))
	for i in 6:
		put("actor_%d"%i,actor(i),Vector2i(i,8))
	# Native wall piece: copied unchanged, useful for constructing tall piers.
	put("pier",a4.get_region(Rect2i(480,144,48,96)),Vector2i(12,4))
	assert(canvas.save_png(OUT+"Academy_Native48.png")==OK)
	assert(ResourceSaver.save(ImageTexture.create_from_image(canvas),OUT+"Academy_Native48.res")==OK)
	var f:=FileAccess.open(OUT+"catalog.json",FileAccess.WRITE)
	f.store_string(JSON.stringify({"tile_size":48,"atlas_size":[960,768],"method":"Original-size copy and direct pixel drawing; no image resampling", "objects":objects},"\t"))
	return objects

func put(label:String,part:Image,cell:Vector2i) -> void:
	assert(part.get_width()%48==0 and part.get_height()%48==0)
	canvas.blit_rect(part,Rect2i(Vector2i.ZERO,part.get_size()),cell*48)
	objects[label]=[cell.x,cell.y,part.get_width()/48,part.get_height()/48]

func blank(w:int,h:int) -> Image:
	var result:=Image.create(w,h,false,Image.FORMAT_RGBA8)
	result.fill(Color.TRANSPARENT)
	return result

func rect(im:Image,r:Rect2i,col:Color) -> void:
	im.fill_rect(r.intersection(Rect2i(Vector2i.ZERO,im.get_size())),col)

func pixel(im:Image,p:Vector2i,col:Color) -> void:
	if p.x>=0 and p.y>=0 and p.x<im.get_width() and p.y<im.get_height(): im.set_pixelv(p,col)

func line(im:Image,a:Vector2i,b:Vector2i,col:Color,width:int=1) -> void:
	var length:=maxi(absi(b.x-a.x),absi(b.y-a.y))
	for i in range(length+1):
		var point:=Vector2i(Vector2(a).lerp(Vector2(b),float(i)/maxi(1,length)).round())
		rect(im,Rect2i(point-Vector2i(width/2,width/2),Vector2i(width,width)),col)

func poly(im:Image,points:Array,col:Color) -> void:
	var shape:=PackedVector2Array()
	for p in points: shape.append(Vector2(p))
	var left:=im.get_width()
	var top:=im.get_height()
	var right:=0
	var bottom:=0
	for p in points:
		left=mini(left,p.x); top=mini(top,p.y); right=maxi(right,p.x); bottom=maxi(bottom,p.y)
	for y in range(maxi(0,top),mini(im.get_height(),bottom+1)):
		for x in range(maxi(0,left),mini(im.get_width(),right+1)):
			if Geometry2D.is_point_in_polygon(Vector2(x+0.5,y+0.5),shape): im.set_pixel(x,y,col)

func ellipse(im:Image,center:Vector2i,radius:Vector2i,col:Color) -> void:
	for y in range(center.y-radius.y,center.y+radius.y+1):
		for x in range(center.x-radius.x,center.x+radius.x+1):
			if Vector2(float(x-center.x)/radius.x,float(y-center.y)/radius.y).length_squared()<=1.0: pixel(im,Vector2i(x,y),col)

func clock() -> Image:
	var im:=blank(192,192)
	for x in [42,54,137,149]:
		rect(im,Rect2i(x,12,7,168),palette.ink)
		rect(im,Rect2i(x+1,12,4,168),palette.brass)
		line(im,Vector2i(x+1,12),Vector2i(x+1,179),palette.light)
		for y in [30,120,169]: rect(im,Rect2i(x-2,y,11,5),Color("6e603b"))
	for y in [68,119]:
		for side in [-1,1]:
			var c:=Vector2i(96+side*67,y)
			poly(im,[c+Vector2i(side*18,-23),c+Vector2i(-side*17,-5),c+Vector2i(-side*17,5),c+Vector2i(side*18,23)],palette.ink)
			poly(im,[c+Vector2i(side*16,-20),c+Vector2i(-side*15,-3),c+Vector2i(-side*15,3),c+Vector2i(side*16,20)],palette.brass)
			ellipse(im,c+Vector2i(side*15,0),Vector2i(9,23),palette.ink)
			ellipse(im,c+Vector2i(side*15,0),Vector2i(7,20),Color("685934"))
			ellipse(im,c+Vector2i(side*15,0),Vector2i(4,15),Color("393327"))
	for spec in [[49,"25231f"],[46,"766342"],[43,"b6a06c"],[40,"544b38"],[37,"c0b493"]]:
		ellipse(im,Vector2i(96,65),Vector2i(spec[0],spec[0]),Color(spec[1]))
	for i in 12:
		var angle:=i*TAU/12-PI/2
		var outer:=Vector2i(Vector2(96,65)+Vector2(cos(angle),sin(angle))*34)
		var inner:=Vector2i(Vector2(96,65)+Vector2(cos(angle),sin(angle))*28)
		line(im,inner,outer,Color("484235"),2)
	line(im,Vector2i(96,65),Vector2i(96,35),palette.ink,2)
	line(im,Vector2i(96,65),Vector2i(114,74),palette.ink,3)
	ellipse(im,Vector2i(96,65),Vector2i(4,4),palette.brass)
	for c in [Vector2i(78,130),Vector2i(115,148),Vector2i(76,164)]:
		for i in 12:
			var p:Vector2i=c+Vector2i(Vector2(cos(i*TAU/12),sin(i*TAU/12))*12)
			rect(im,Rect2i(p-Vector2i(2,2),Vector2i(5,5)),palette.brass)
		ellipse(im,c,Vector2i(11,11),palette.brass)
		ellipse(im,c,Vector2i(8,8),palette.ink)
		for i in 6: line(im,c,c+Vector2i(Vector2(cos(i*TAU/6),sin(i*TAU/6))*9),palette.brass)
	return im

func portal(source:Image) -> Image:
	var im:=blank(144,192)
	poly(im,[Vector2i(4,191),Vector2i(4,64),Vector2i(28,24),Vector2i(72,0),Vector2i(116,24),Vector2i(140,64),Vector2i(140,191)],palette.stone)
	for y in 192:
		for x in 144:
			if im.get_pixel(x,y).a>0:
				var c:=source.get_pixel(480+x%96,144+y%96)
				var v:=c.get_luminance()
				im.set_pixel(x,y,Color(v*0.67+0.13,v*0.62+0.12,v*0.52+0.10))
	var opening:=[Vector2i(28,191),Vector2i(28,75),Vector2i(42,44),Vector2i(72,20),Vector2i(102,44),Vector2i(116,75),Vector2i(116,191)]
	poly(im,opening,Color("22252a"))
	for i in 7:
		rect(im,Rect2i(30,110+i*11,84,8),Color(0.16+i*0.008,0.17+i*0.008,0.19+i*0.008))
		line(im,Vector2i(30,118+i*11),Vector2i(113,118+i*11),Color("35373a"))
	for i in range(opening.size()-1): line(im,opening[i],opening[i+1],Color("ada28b"),2)
	for x in [7,122]:
		rect(im,Rect2i(x,74,15,118),Color("696253"))
		rect(im,Rect2i(x+2,77,4,109),Color("9b907b"))
		for y in [76,139,181]: rect(im,Rect2i(x-3,y,21,6),Color("494439"))
	return im

func flag(kind:int) -> Image:
	var im:=blank(96,144)
	var base:Color=[Color("612c30"),Color("682c30"),Color("283c51"),Color("343534"),Color("5c2b32")][kind]
	var ink:Color=Color("b4ad92") if kind in [2,3] else Color("af945d")
	var shape:=[Vector2i(21,12),Vector2i(74,12),Vector2i(74,112),Vector2i(48,132),Vector2i(21,112)]
	poly(im,shape,base)
	for y in range(12,132):
		for x in range(21,75):
			if im.get_pixel(x,y).a>0:
				var fold:float=[-0.15,-0.08,0.035,0.0,-0.05,0.04,0.0,-0.10][mini(7,(x-21)/7)]
				im.set_pixel(x,y,base.lightened(fold) if fold>=0 else base.darkened(-fold))
	for i in range(shape.size()-1): line(im,shape[i],shape[i+1],ink.darkened(0.20))
	line(im,shape[-1],shape[0],ink.darkened(0.20))
	rect(im,Rect2i(13,8,70,4),Color("34302a"))
	line(im,Vector2i(14,8),Vector2i(82,8),ink)
	for x in [12,83]: ellipse(im,Vector2i(x,10),Vector2i(3,4),ink.darkened(0.15))
	match kind:
		1:
			rect(im,Rect2i(45,70,5,19),ink)
			line(im,Vector2i(39,69),Vector2i(55,69),ink,2)
			poly(im,[Vector2i(44,66),Vector2i(40,59),Vector2i(48,47),Vector2i(47,57),Vector2i(54,52),Vector2i(53,63),Vector2i(49,66)],ink)
			for side in [-1,1]:
				line(im,Vector2i(48,94),Vector2i(48+side*19,63),ink)
				for j in 5:
					var c:=Vector2i(48+side*(6+j*3),88-j*5)
					ellipse(im,c,Vector2i(3,2),ink)
		2:
			line(im,Vector2i(48,48),Vector2i(28,82),ink,2)
			line(im,Vector2i(28,82),Vector2i(68,82),ink,2)
			line(im,Vector2i(68,82),Vector2i(48,48),ink,2)
			ellipse(im,Vector2i(48,72),Vector2i(11,5),ink)
			ellipse(im,Vector2i(48,72),Vector2i(8,3),base)
			ellipse(im,Vector2i(48,72),Vector2i(3,3),ink)
			for side in [-1,1]:
				line(im,Vector2i(48+side*8,87),Vector2i(48+side*18,90),ink,2)
				ellipse(im,Vector2i(48+side*19,86),Vector2i(4,4),ink)
				ellipse(im,Vector2i(48+side*19,86),Vector2i(2,2),base)
		3:
			poly(im,[Vector2i(29,79),Vector2i(42,59),Vector2i(48,62),Vector2i(54,59),Vector2i(67,79),Vector2i(51,69),Vector2i(48,75),Vector2i(45,69)],ink)
			for row in 3:
				for x in range(29,67): pixel(im,Vector2i(x,84+row*5+roundi(sin((x-29)*0.28)*2)),ink)
			for j in 12: pixel(im,Vector2i(48+roundi(sin(j*0.5)*3),57-j),ink)
		0:
			ellipse(im,Vector2i(48,73),Vector2i(19,19),ink)
			ellipse(im,Vector2i(48,73),Vector2i(17,17),base)
			for j in 100:
				var a:=j*0.07
				var rad:=12.0-j*0.075
				rect(im,Rect2i(Vector2i(48+cos(a)*rad,74+sin(a)*rad),Vector2i(3,3)),ink)
			poly(im,[Vector2i(48,59),Vector2i(57,57),Vector2i(64,61),Vector2i(55,64)],ink)
			line(im,Vector2i(50,59),Vector2i(48,54),ink)
			line(im,Vector2i(56,58),Vector2i(58,53),ink)
		4:
			ellipse(im,Vector2i(43,64),Vector2i(6,7),ink)
			poly(im,[Vector2i(45,69),Vector2i(53,69),Vector2i(57,84),Vector2i(46,84)],ink)
			for limb in [[45,72,33,65],[45,77,34,75],[48,84,38,94],[53,83,62,91]]:
				line(im,Vector2i(limb[0],limb[1]),Vector2i(limb[2],limb[3]),ink,3)
			line(im,Vector2i(56,80),Vector2i(64,73),ink,2)
			line(im,Vector2i(64,73),Vector2i(62,59),ink,2)
	return im

func table(_source:Image) -> Image:
	var im:=blank(144,144)
	# Draw planks directly in the final 144px canvas.
	for y in range(5,91):
		for x in range(2,142): im.set_pixel(x,y,Color("77553b").lightened(float((x*7+y*3)%11)/160.0))
	for y in [25,47,69]: line(im,Vector2i(2,y),Vector2i(141,y),Color("624630"))
	for y in [5,7,88,91,96]: line(im,Vector2i(2,y),Vector2i(141,y),Color("38291f") if y%2==0 else Color("977454"),2)
	for x in [3,140]: line(im,Vector2i(x,6),Vector2i(x,94),Color("3e2c20"),2)
	for x in [8,126]:
		rect(im,Rect2i(x,95,10,44),Color("33261c"))
		rect(im,Rect2i(x+2,97,5,37),Color("76553a"))
		rect(im,Rect2i(x,132,10,5),Color("977454"))
	return im

func fire() -> Image:
	var im:=blank(48,48)
	for j in 6:
		var x:=7+j*6
		var h: int=[19,28,34,25,31,18][j]
		poly(im,[Vector2i(x-4,43),Vector2i(x-3,43-h/2),Vector2i(x+1,43-h),Vector2i(x+5,43-h/2),Vector2i(x+6,43)],Color("aa4c22"))
		poly(im,[Vector2i(x-2,43),Vector2i(x+1,43-h+9),Vector2i(x+3,43)],Color("e4a349"))
	line(im,Vector2i(5,42),Vector2i(42,42),Color("30251d"),3)
	return im

func seal() -> Image:
	var im:=blank(96,96)
	var c:=Vector2i(48,48)
	for spec in [[44,"8e7d54"],[42,"59594e"],[37,"9b8a60"],[35,"535855"]]: ellipse(im,c,Vector2i(spec[0],spec[0]),Color(spec[1]))
	for i in 8:
		var a:=i*TAU/8
		var end:=c+Vector2i(Vector2(cos(a),sin(a))*40)
		poly(im,[end,c+Vector2i(Vector2(cos(a+PI/2),sin(a+PI/2))*5),c],Color("b0a078"))
		poly(im,[end,c+Vector2i(Vector2(cos(a-PI/2),sin(a-PI/2))*5),c],Color("796b4a"))
	ellipse(im,c,Vector2i(7,7),Color("9f9272"))
	return im

func actor(kind:int) -> Image:
	var im:=blank(48,96)
	var hair:Color=[Color("282625"),Color("282625"),Color("a9552e"),Color("322a26"),Color("a6a3a0"),Color("ae4b27")][kind]
	var cloth:Color=[Color("6d5039"),Color("6d5039"),Color("46503c"),Color("d0cab5"),Color("363735"),Color("9c5c32")][kind]
	var ink:=Color("262420")
	var skin:=Color("c7a27e")
	var seated:=kind in [1,2,5]
	# Silhouette first, then light-facing planes. Each mark is an actual pixel.
	poly(im,[Vector2i(15,39),Vector2i(33,39),Vector2i(40,56),Vector2i(37,75),Vector2i(11,75),Vector2i(8,56)],ink)
	poly(im,[Vector2i(16,41),Vector2i(32,41),Vector2i(36,72),Vector2i(12,72)],cloth)
	rect(im,Rect2i(14,46,5,24),cloth.lightened(0.12))
	rect(im,Rect2i(29,46,6,25),cloth.darkened(0.16))
	if not seated:
		for x in [16,27]:
			rect(im,Rect2i(x,71,7,15),Color("3b3530"))
			rect(im,Rect2i(x-1,83,9,5),ink)
	else:
		rect(im,Rect2i(6,71,36,25),Color.TRANSPARENT)
	for side in [-1,1]:
		var x:int=24+side*13
		line(im,Vector2i(x,46),Vector2i(x+side*3,61),ink,8)
		line(im,Vector2i(x,47),Vector2i(x+side*3,60),cloth,5)
		ellipse(im,Vector2i(x+side*3,63),Vector2i(3,4),skin)
	ellipse(im,Vector2i(24,27),Vector2i(13,16),ink)
	ellipse(im,Vector2i(24,28),Vector2i(10,13),skin)
	ellipse(im,Vector2i(24,20),Vector2i(13,11),hair)
	if kind==0:
		ellipse(im,Vector2i(24,25),Vector2i(12,13),hair)
		line(im,Vector2i(14,48),Vector2i(34,48),cloth.lightened(0.16))
		line(im,Vector2i(13,64),Vector2i(35,64),Color("3d3024"),2)
	else:
		rect(im,Rect2i(17,29,4,2),ink)
		rect(im,Rect2i(28,29,4,2),ink)
		pixel(im,Vector2i(18,29),Color("eee0c1")); pixel(im,Vector2i(29,29),Color("eee0c1"))
		line(im,Vector2i(22,37),Vector2i(26,37),Color("805d4a"))
		poly(im,[Vector2i(18,41),Vector2i(24,48),Vector2i(29,41)],Color("c3b99b"))
		line(im,Vector2i(24,47),Vector2i(24,66),ink)
	for i in 8:
		var x:=12+i*3
		poly(im,[Vector2i(x,19),Vector2i(x+1,9+(i*7)%8),Vector2i(x+5,21)],hair)
	if kind==2:
		for side in [-1,1]:
			for j in 5:
				var pos:=Vector2i(24+side*(11+j%2*2),21+j*5)
				ellipse(im,pos,Vector2i(5,5),hair.darkened(0.15))
				ellipse(im,pos-Vector2i(1,1),Vector2i(3,3),hair.lightened(0.1))
		for x in [19,29]:
			ellipse(im,Vector2i(x,30),Vector2i(5,4),Color("86734a"))
			ellipse(im,Vector2i(x,30),Vector2i(3,2),skin)
			pixel(im,Vector2i(x,30),ink)
	if kind==3:
		rect(im,Rect2i(14,61,22,19),Color("554234"))
		for y in [47,53,59]:
			for x in [21,28]: rect(im,Rect2i(x,y,2,2),Color("8b7446"))
		for y in range(34,63,4): ellipse(im,Vector2i(12,y),Vector2i(3,3),hair)
	if kind==4:
		for x in [10,36]: rect(im,Rect2i(x,23,3,12),palette.brass)
		for y in [48,55,62]: pixel(im,Vector2i(27,y),palette.brass)
		line(im,Vector2i(8,54),Vector2i(8,69),palette.brass)
		line(im,Vector2i(5,48),Vector2i(5,55),palette.brass)
		line(im,Vector2i(11,48),Vector2i(11,55),palette.brass)
		line(im,Vector2i(5,55),Vector2i(11,55),palette.brass)
	if kind==5:
		for x in [20,29]:
			ellipse(im,Vector2i(x,46),Vector2i(5,4),palette.brass)
			ellipse(im,Vector2i(x,46),Vector2i(3,2),Color("454944"))
	if seated or kind==3:
		ellipse(im,Vector2i(24,68),Vector2i(10,5),ink)
		ellipse(im,Vector2i(24,67),Vector2i(9,4),Color("b5b4a5"))
		ellipse(im,Vector2i(24,66),Vector2i(7,2),Color("c28d42"))
	return im

func counter() -> Image:
	var im:=blank(144,96)
	rect(im,Rect2i(2,20,140,72),Color("473326"))
	for x in [5,51,97]:
		rect(im,Rect2i(x,25,41,62),Color("77553b"))
		rect(im,Rect2i(x+4,29,33,54),Color("624530"))
		rect(im,Rect2i(x+31,49,3,7),Color("b99b5d"))
	rect(im,Rect2i(0,5,144,16),Color("a49c82"))
	line(im,Vector2i(0,5),Vector2i(143,5),Color("c1b89d"),2)
	line(im,Vector2i(0,20),Vector2i(143,20),Color("514a3d"),2)
	return im
