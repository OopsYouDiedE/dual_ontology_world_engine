extends SceneTree

func _initialize():
    var base = "res://ShareAsset/assets/environment/research_hall/"
    var entries = [
        ["floor", "floor/floor_rug_topdown_sheet.png", Rect2i(48,95,799,809), Vector2i(1536,1024), false],
        ["rug", "floor/floor_rug_topdown_sheet.png", Rect2i(875,134,634,468), Vector2i(1536,1024), true],
        ["wall", "walls/wall_openings_sheet.png", Rect2i(34,104,508,560), Vector2i(1774,887), false],
        ["door", "walls/wall_openings_sheet.png", Rect2i(901,130,432,536), Vector2i(1774,887), true],
        ["window", "walls/wall_openings_sheet.png", Rect2i(1354,99,227,568), Vector2i(1774,887), true],
        ["column", "walls/wall_openings_sheet.png", Rect2i(1618,88,130,582), Vector2i(1774,887), true],
        ["shelf", "furniture/furniture_sheet.png", Rect2i(200,5,289,358), Vector2i(1536,1024), true],
        ["board", "furniture/furniture_sheet.png", Rect2i(148,660,374,340), Vector2i(1536,1024), true],
        ["stove", "furniture/furniture_sheet.png", Rect2i(1144,365,143,346), Vector2i(1536,1024), true],
        ["plant", "furniture/furniture_sheet.png", Rect2i(638,661,254,351), Vector2i(1536,1024), true],
        ["chess", "furniture/furniture_sheet.png", Rect2i(603,73,319,288), Vector2i(1536,1024), true],
        ["chair_front", "furniture/furniture_sheet.png", Rect2i(1119,5,202,358), Vector2i(1536,1024), true],
        ["chair_back", "furniture/furniture_sheet.png", Rect2i(703,365,185,294), Vector2i(1536,1024), true],
        ["desk", "furniture/furniture_sheet.png", Rect2i(136,411,407,245), Vector2i(1536,1024), true],
        ["books", "furniture/furniture_sheet.png", Rect2i(1245,825,178,152), Vector2i(1536,1024), true],
        ["ink", "furniture/furniture_sheet.png", Rect2i(1145,739,111,234), Vector2i(1536,1024), true],
        ["banner_red", "hanging/banners_lamp_sheet.png", Rect2i(97,38,365,711), Vector2i(1983,793), true],
        ["banner_black", "hanging/banners_lamp_sheet.png", Rect2i(577,38,365,711), Vector2i(1983,793), true],
        ["banner_blue", "hanging/banners_lamp_sheet.png", Rect2i(1057,38,365,711), Vector2i(1983,793), true],
        ["lamp", "hanging/banners_lamp_sheet.png", Rect2i(1543,33,362,700), Vector2i(1983,793), true],
    ]
    for entry in entries:
        var sheet = Image.load_from_file(base + entry[1])
        var ratio = Vector2(sheet.get_size()) / Vector2(entry[3])
        var region = Rect2i(Vector2(entry[2].position) * ratio, Vector2(entry[2].size) * ratio)
        var image = sheet.get_region(region)
        image.convert(Image.FORMAT_RGBA8)
        if entry[4]:
            var data = image.get_data()
            for i in range(0, data.size(), 4):
                var low = mini(data[i], mini(data[i+1], data[i+2]))
                var high = maxi(data[i], maxi(data[i+1], data[i+2]))
                if low > 112 and high - low < 18:
                    data[i+3] = 0
            image.set_data(image.get_width(), image.get_height(), false, Image.FORMAT_RGBA8, data)
        image.save_png(base + "cutouts/" + entry[0] + ".png")
    print("Prepared 20 reusable textures; scene nodes reference these files directly.")
    quit()
