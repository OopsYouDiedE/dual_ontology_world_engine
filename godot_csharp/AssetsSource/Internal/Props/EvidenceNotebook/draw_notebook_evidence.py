"""Integer-pixel originals for the notebook evidence illustrations."""
from pathlib import Path
from PIL import Image, ImageDraw

SOURCE = Path(__file__).resolve().parent
RUNTIME = SOURCE.parents[3] / "ShareAsset/Internal/Props/EvidenceNotebook"
INK = (91, 83, 69, 255)
PENCIL = (132, 121, 101, 255)
LIGHT = (170, 158, 135, 255)


def line(draw, points, color=PENCIL):
    draw.line(points, fill=color, width=1)


def publish(image, name):
    RUNTIME.mkdir(parents=True, exist_ok=True)
    image.save(SOURCE / name)
    image.save(RUNTIME / name)


image = Image.new("RGBA", (128, 96))
draw = ImageDraw.Draw(image)
# A triangular wood wedge. Its upper slope ends at the thin right tip.
line(draw, [(15, 41), (29, 33), (61, 64), (48, 72), (15, 41)], INK)
line(draw, [(15, 41), (15, 65), (48, 72)], INK)
line(draw, [(17, 44), (17, 61)], LIGHT)
line(draw, [(19, 49), (25, 54), (28, 56)], LIGHT)
line(draw, [(22, 62), (33, 66), (38, 67)], PENCIL)
line(draw, [(27, 42), (33, 48), (34, 50)], LIGHT)
line(draw, [(34, 43), (40, 50)], PENCIL)
line(draw, [(43, 57), (49, 62)], LIGHT)
# Selective dusty marks on the slope; clear gaps keep the drawing airy.
for x, y in [(26, 42), (30, 44), (32, 45), (29, 47), (35, 48),
             (33, 50), (37, 51), (36, 53), (39, 54), (41, 54),
             (39, 57), (43, 59), (46, 60), (42, 58)]:
    draw.point((x, y), fill=INK)
line(draw, [(30, 47), (31, 48)])
line(draw, [(37, 54), (38, 55)])
# Open paper, with short creases instead of a filled texture or cast shadow.
line(draw, [(73, 22), (111, 28), (117, 66), (113, 70),
            (80, 75), (69, 47), (73, 22)], INK)
line(draw, [(73, 24), (84, 47), (80, 73)], LIGHT)
line(draw, [(86, 46), (107, 30)], LIGHT)
line(draw, [(86, 49), (113, 65)], LIGHT)
line(draw, [(78, 27), (90, 29)], LIGHT)
line(draw, [(91, 71), (106, 69)], LIGHT)
# A partial wedge-face transfer, interrupted rather than a solid grey patch.
line(draw, [(88, 39), (96, 43), (102, 51)], PENCIL)
line(draw, [(104, 54), (107, 59), (99, 56)], PENCIL)
line(draw, [(96, 54), (90, 50), (87, 44)], LIGHT)
for x, y in [(91, 43), (94, 45), (93, 47), (97, 47), (96, 49),
             (99, 50), (98, 52), (102, 55), (94, 50), (101, 53)]:
    draw.point((x, y), fill=PENCIL)
line(draw, [(91, 44), (93, 46)], LIGHT)
line(draw, [(97, 51), (99, 53)], LIGHT)
publish(image, "wedge-paper.png")

image = Image.new("RGBA", (96, 40))
draw = ImageDraw.Draw(image)
# Facing torn edges share a stepped contour and a narrow inspection gap.
left_edge = [(44, 7), (42, 7), (42, 10), (45, 10), (45, 13),
             (43, 13), (43, 16), (46, 16), (46, 19), (43, 19),
             (43, 22), (45, 22), (45, 25), (42, 25), (42, 29), (44, 29), (44, 33)]
right_edge = [(x + 8, y) for x, y in left_edge]
line(draw, [(8, 6), (44, 7)], INK)
line(draw, left_edge, INK)
line(draw, [(44, 33), (9, 34), (7, 25), (8, 6)], INK)
line(draw, right_edge, INK)
line(draw, [(52, 7), (87, 6), (89, 30), (87, 34), (52, 33)], INK)
line(draw, [(12, 9), (30, 9)], LIGHT)
line(draw, [(61, 9), (81, 9)], LIGHT)
line(draw, [(13, 30), (31, 31)], LIGHT)
line(draw, [(65, 31), (82, 31)], LIGHT)
for x, y in [(40, 10), (47, 18), (41, 25), (55, 14), (50, 23), (54, 30)]:
    draw.point((x, y), fill=LIGHT)
publish(image, "tear-detail.png")


# The following notebook thumbnails use only observed object shapes. Written
# information stays in live UI text; these marks do not encode names or answers.
def canvas():
    im = Image.new("RGBA", (128, 96))
    return im, ImageDraw.Draw(im)


def rect(d, box, color=INK):
    d.rectangle(box, outline=color, width=1)


def sheet(d, x, y, w, h, torn=False):
    if torn:
        line(d, [(x,y),(x+w,y),(x+w,y+h-7),(x+w-5,y+h-5),
                 (x+w-9,y+h-6),(x+w-14,y+h-3),(x+w-18,y+h-5),
                 (x+8,y+h-2),(x,y+h),(x,y)], INK)
    else:
        rect(d,(x,y,x+w,y+h))
    for yy, end in [(y+8,w-8),(y+13,w-14),(y+23,w-10)]:
        if yy < y+h-5:
            line(d, [(x+5,yy),(x+end,yy)], LIGHT)
    if h > 35:
        line(d,[(x+8,y+h-10),(x+12,y+h-13),(x+15,y+h-9),
                (x+20,y+h-12),(x+23,y+h-10)],PENCIL)


def dust(d, x,y,w,h,count=22):
    for n in range(count):
        d.point((x+(n*17+n//3)%w,y+(n*11+n//4)%h),fill=PENCIL)


def wedge(d,x,y):
    line(d,[(x,y),(x+31,y-16),(x+36,y+7),(x+5,y+9),(x,y)],INK)
    line(d,[(x+31,y-16),(x+31,y+3),(x+5,y+9)],PENCIL)
    for dx,dy in [(9,0),(17,-4),(22,-8)]:
        line(d,[(x+dx,y+dy),(x+dx+5,y+dy-2)],LIGHT)


def token(d,x,y):
    line(d,[(x,y),(x+17,y),(x+17,y+5),(x+23,y+5),
            (x+23,y+22),(x,y+22),(x,y)],INK)
    line(d,[(x+5,y+8),(x+13,y+8)],PENCIL)


def handle(d,x,y):
    rect(d,(x,y,x+24,y+5))
    line(d,[(x+12,y+5),(x+12,y+27),(x+17,y+27)],INK)


def basin(d,deep=False):
    # Permanent plumbing ends above the basin: no water or arrows are drawn.
    for x in [29,94]:
        rect(d,(x,11,x+7,42))
        line(d,[(x-2,11),(x+9,11)],PENCIL)
    rect(d,(23,26,42,36))
    for x in range(26,41,4): line(d,[(x,28),(x,34)],LIGHT)
    line(d,[(14,53),(113,53),(106,76),(23,76),(14,53)],INK)
    line(d,[(21,58),(29,69),(100,69),(106,58)],PENCIL)
    dust(d,30,67,69,3,24)
    d.ellipse((61,72,67,75),outline=PENCIL)
    if deep:
        rect(d,(49,14,77,34))
        for x in range(53,77,4): line(d,[(x,17),(x,31)],LIGHT)
        for y in range(18,33,4): line(d,[(52,y),(74,y)],LIGHT)
        dust(d,53,17,20,14,18)


def draw_evidence(evidence_id):
    im,d = canvas()
    if evidence_id == "E01":
        # Claim cabinet, receipt and numbered credential; no rewards invented.
        rect(d,(12,21,65,73))
        for y in [26,43,60]:
            rect(d,(17,y,59,y+11)); line(d,[(34,y+5),(42,y+5)])
        sheet(d,77,18,33,49)
        d.ellipse((89,52,98,61),outline=INK)
        line(d,[(80,77),(94,72),(112,77)],LIGHT)
    elif evidence_id == "E02":
        sheet(d,17,20,47,59); sheet(d,65,20,47,59)
        for y in [28,39,50,61,72]: line(d,[(61,y),(67,y)],PENCIL)
        d.ellipse((56,42,71,56),outline=PENCIL)
    elif evidence_id == "E03":
        rect(d,(21,56,80,73)); handle(d,33,24)
        line(d,[(45,51),(51,53),(41,56),(51,59),(41,62),(51,65)],PENCIL)
        rect(d,(88,40,98,45)); rect(d,(105,40,115,45))
        line(d,[(84,42),(88,42)],INK)
        sheet(d,81,13,30,20)
    elif evidence_id == "E04":
        # Door gap and copper pipe across a wall corner.
        line(d,[(14,19),(45,19),(45,75),(14,75)],INK)
        line(d,[(50,19),(50,75)],LIGHT)
        line(d,[(67,23),(89,23),(89,52),(113,52)],INK)
        line(d,[(67,29),(83,29),(83,58),(113,58)],INK)
        line(d,[(72,70),(72,40),(78,40)],LIGHT)
        for x in [70,103]: line(d,[(x,51),(x,59)],PENCIL)
    elif evidence_id == "E05":
        rect(d,(19,17,46,24))
        line(d,[(23,24),(23,66),(28,68),(32,64),(35,70),(40,65),
                (43,68),(43,24)],INK)
        for x in [26,32,38]: line(d,[(x,57),(x+1,65)],LIGHT)
        line(d,[(18,73),(31,70),(48,71),(53,77),(16,77),(18,73)],PENCIL)
        wedge(d,72,57)
    elif evidence_id == "E06":
        rect(d,(14,18,113,61)); line(d,[(14,65),(113,65)],INK)
        for x in [26,48,74,101]: line(d,[(x,21),(x,52)],LIGHT)
        line(d,[(49,75),(78,68),(84,72),(54,79),(49,75)],LIGHT)
        dust(d,17,71,28,9,23); dust(d,88,71,24,9,22)
        rect(d,(24,35,43,39)); rect(d,(85,35,101,44))
    elif evidence_id == "E07":
        # Existing approved original kept byte-for-byte; center it unchanged.
        im = Image.open(SOURCE / "wedge-paper.png").convert("RGBA")
    elif evidence_id == "E08":
        line(d,[(24,22),(93,28),(102,78),(30,75),(24,22)],INK)
        line(d,[(24,22),(27,37),(96,44)],PENCIL)
        line(d,[(27,38),(94,44)],INK)
        rect(d,(60,18,75,38)); rect(d,(64,23,71,33),PENCIL)
        dust(d,31,35,55,8,30)
        for x,y in [(37,55),(67,64),(86,58),(49,69)]:
            line(d,[(x,y),(x+4,y-2),(x+8,y+1)],PENCIL)
        for x in [33,46,83]: line(d,[(x,48),(x+3,70)],LIGHT)
    elif evidence_id in ["E09","E09.deep"]:
        basin(d,evidence_id.endswith("deep"))
    elif evidence_id == "E09.completion":
        sheet(d,17,18,42,58); sheet(d,68,18,42,58)
        for x in [22,73]:
            line(d,[(x,39),(x+32,39)],PENCIL)
            line(d,[(x+3,68),(x+26,68)],LIGHT)
    elif evidence_id == "E10":
        rect(d,(16,18,69,75))
        # The rack retains an empty handle outline, not a reconstructed tool.
        handle(d,29,33)
        sheet(d,79,28,33,47)
        line(d,[(22,67),(44,67)],LIGHT)
    elif evidence_id == "E11":
        rect(d,(14,17,66,75)); rect(d,(34,16,47,22),PENCIL)
        sheet(d,69,28,37,49); sheet(d,73,23,37,49)
        rect(d,(81,20,91,27),PENCIL)
    elif evidence_id == "E12":
        # Material anchors only; no invented pictures of recalled events.
        sheet(d,17,24,44,50)
        line(d,[(39,25),(39,71)],LIGHT)
        wedge(d,77,59)
    elif evidence_id == "E13":
        rect(d,(14,20,91,62))
        for x in [22,58]:
            rect(d,(x,26,x+23,30)); rect(d,(x+8,40,x+16,46),PENCIL)
            line(d,[(x+11,33),(x+11,38)],LIGHT)
        rect(d,(21,53,83,58),PENCIL)
        # Paper tape's existing notched imprint, separate blank outlet.
        rect(d,(18,70,81,87),PENCIL)
        line(d,[(24,74),(34,74),(34,77),(38,77),(38,83),(24,83),(24,74)],INK)
        rect(d,(99,48,115,54)); rect(d,(101,51,112,60),PENCIL)
    elif evidence_id == "E14":
        sheet(d,15,34,27,23); rect(d,(11,62,44,74),PENCIL)
        for y in [14,33,52,71]:
            sheet(d,57,y,54,14)
            line(d,[(62,y+7),(70,y+5),(75,y+9),(83,y+6)],PENCIL)
    elif evidence_id == "E15":
        line(d,[(14,29),(56,21),(69,32),(69,64),(25,73),(14,60),(14,29)],INK)
        line(d,[(14,29),(25,39),(69,32),(25,39),(25,73)],PENCIL)
        rect(d,(35,57,48,61),PENCIL)
        line(d,[(34,60),(39,62),(46,59)],LIGHT)
        # Partial palm and fingers on dusty drawer floor, not an identity mark.
        line(d,[(32,33),(33,40),(39,42),(45,39),(47,33)],PENCIL)
        for x,y in [(33,30),(37,28),(41,27),(45,29)]:
            line(d,[(x,y),(x+1,y+7)],PENCIL)
        dust(d,26,27,30,17,15)
        line(d,[(15,21),(18,18),(20,20),(23,17)],PENCIL)
        line(d,[(86,26),(86,58),(113,58),(113,26)],INK)
        line(d,[(83,59),(118,59),(118,69),(83,69),(83,59)],INK)
        line(d,[(87,69),(86,82)],PENCIL); line(d,[(114,69),(115,82)],PENCIL)
        token(d,92,35); dust(d,95,40,15,12,10)
    elif evidence_id == "E16":
        sheet(d,31,14,63,70)
        for y in [44,50,56]: line(d,[(38,y),(82,y)],LIGHT)
        d.ellipse((73,66,86,78),outline=PENCIL)
    elif evidence_id == "ROOM_COAT":
        line(d,[(48,19),(61,13),(74,19),(89,29),(79,42),(74,35),
                (77,74),(43,74),(46,35),(40,42),(30,29),(48,19)],INK)
        line(d,[(48,19),(60,31),(74,19)],PENCIL)
        line(d,[(60,31),(60,70)],LIGHT); rect(d,(65,42,72,50),LIGHT)
        sheet(d,89,52,27,28); dust(d,108,73,7,5,9)
    elif evidence_id == "ROOM_DESK":
        rect(d,(16,47,113,69)); line(d,[(21,69),(21,83)],INK)
        line(d,[(108,69),(108,83)],INK)
        rect(d,(24,37,64,43),PENCIL)
        d.ellipse((29,30,57,38),outline=PENCIL)
        rect(d,(73,24,88,42)); d.arc((84,27,97,38),260,100,fill=INK)
        sheet(d,38,55,47,18,True)
    elif evidence_id == "ROOM_DOOR":
        rect(d,(30,12,95,84)); rect(d,(35,17,90,79),LIGHT)
        rect(d,(72,23,86,31),PENCIL)
        # Inner bolt rests beside its keeper, with visible separation.
        rect(d,(54,47,71,51)); rect(d,(77,46,81,52),PENCIL)
        for y in [27,65]: rect(d,(30,y,34,y+6),PENCIL)
    elif evidence_id == "ROOM_WINDOW":
        rect(d,(22,14,106,77)); rect(d,(26,18,102,73),LIGHT)
        for x in [42,64,86]: line(d,[(x,15),(x,76)],INK)
        line(d,[(23,43),(105,43)],PENCIL)
        for x,y,w in [(28,30,10),(49,57,10),(70,26,11),(90,61,8)]:
            line(d,[(x,y),(x+w,y)],LIGHT)
    else:
        raise ValueError(evidence_id)
    return im


EVIDENCE_IDS = [f"E{n:02}" for n in range(1,17)] + [
    "E09.deep", "E09.completion", "ROOM_COAT", "ROOM_DESK", "ROOM_DOOR", "ROOM_WINDOW"]
for evidence_id in EVIDENCE_IDS:
    publish(draw_evidence(evidence_id), evidence_id + ".png")
