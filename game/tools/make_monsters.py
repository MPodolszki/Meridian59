#!/usr/bin/env python3
"""Erzeugt server/data/monsters/monsters.json aus dem KOD-Katalog (docs/kod/monsters_catalog.json).

Aufruf (im Ordner game):  python3 tools/make_monsters.py

Übernommen werden alle Kampfmonster, Bosse und beschworenen Wesen (category monster/boss/summoned) mit den Werten
aus den KOD-Klassen: Stufe, Schwierigkeit, Tempo, Karma, Fluchtschwelle, Beutetabelle, Angriffsart, Verhalten,
Resistenzen, Zeichenflags, Spezialangriffe (Gift, Lähmung, Demenz, Ausdauerentzug, Zaubern). Namen und
Beschreibungen sind eigene deutsche Übersetzungen (TEXT unten). Schlüssel = KOD-Klasse in Kleinbuchstaben.
Werte, die von Hand von der Klasse abweichen, stehen in OVERRIDE (mit Begründung).
"""
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parent.parent
catalog = json.loads((root / "docs/kod/monsters_catalog.json").read_text())

# Name, Geschlecht (der/die/das), Beschreibung (vrDesc, sinngemäß übersetzt)
TEXT = {
    "brambles": ("Dornengestrüpp", "n", "Das Dornengestrüpp ragt bedrohlich auf und wartet darauf, jedem die Haut aufzureißen, der vorbeigeht."),
    "ant": ("Ameise", "f", "Blutige Zangen schnappen nach dir. Ameisen sind langsam und fast blind, haben aber einen scharfen Geruchssinn und eine Vorliebe für Menschenfleisch."),
    "avar": ("Avar-Krieger", "m", "Grimmige Krieger, diese flugunfähigen Vogelmenschen. Das Gefieder weist ihn als Mitglied des edlen Peet-Seeeep-Stammes aus."),
    "avarchieftain": ("Avar-Häuptling", "m", "Der Peet-Seeeep-Stamm beherrscht die Südostküste der Insel. Dieser Anführer ist für seine Treffsicherheit mit dem Bogen und seine tödliche Klinge gefürchtet."),
    "avarshaman": ("Avar-Schamane", "m", "Ein Schamane der Peet-Seeeep. Man sagt, die Schamanen blenden sich selbst, damit ihr drittes, magisches Auge wächst. Nur ein Narr legt sich mit diesen Hexern an."),
    "centipede": ("Hundertfüßer", "m", "Leuchtend rote Platten bilden den Panzer dieses riesigen Hundertfüßers. Instinkt, nicht Verstand, treibt ihn auf der Suche nach Nahrung."),
    "cow": ("Kuh", "f", "Eine Zwergkuh. Eine missglückte Zucht hat diese Rinder auf Hüfthöhe schrumpfen lassen; die Bauern von Raza halten sie trotzdem, denn ihre Milch ist süß."),
    "darkangel": ("Dunkler Engel", "m", "Im Schutz der Dunkelheit suchen die Dunklen Engel nach Seelen, die sie verschlingen können. Sie sind das reine Böse – gewiss Diener Qors."),
    "deathspider": ("Schwarze Spinne", "f", "Einst hielt man die schwarze Spinne für ein Schreckgespenst für Kinder – angeblich das Werk böser Zauberei, eine Verschmelzung von Mensch und Insekt. Alle alten Geschichten warnen vor ihrem außerordentlich giftigen Biss."),
    "dragonfly": ("Drachenfliege", "f", "Diese kleinen Echsen wirken ausgesprochen freundlich. Mit ihren hauchdünnen Flügeln können sie kurz auffliegen. In die Enge getrieben, spucken sie übel."),
    "dragonflypet": ("zahme Drachenfliege", "f", "Diese kleinen Echsen wirken ausgesprochen freundlich. Mit ihren hauchdünnen Flügeln können sie kurz auffliegen. In die Enge getrieben, spucken sie übel."),
    "dragonflyqueen": ("Drachenfliegenkönigin", "f", "Die anderen Drachenfliegen scheinen dieses seltsam gefärbte Weibchen zu verehren. Sie mustert dich – aus Neugier oder Hunger, das weißt du nicht."),
    "duskrat": ("Dämmerratte", "f", "Die Luft um die Ratte ist dunkel und schwer vom Bösen. Tod und Krankheit hängen in ihrem fauligen Atem. Sicher ist nur eines: Ihr Biss ist tödlich giftig."),
    "ent": ("lebender Baum", "m", "Ein uralter Geist fließt durch die dunklen Äste dieses Wesens. Es riecht nach altem Holz und fruchtbarer Erde. Man erzählt, er könne Blitze herbeirufen, um den Wald zu schützen."),
    "evilfairy": ("Fey Dirhai", "m", "Dunkel, boshaft und tückisch wollen diese Kobolde den Quilicia-Wald in ewige Finsternis stürzen. Ihre Verwandten, die Fey Elhai, hassen sie glühend."),
    "evilent": ("kranker Baum", "m", "Dieser lebende Baum nährt sich vom verdorbenen Boden; sein Saft ist schwarz vor Hass."),
    "eviltwin": ("böser Zwilling", "m", "Ein Ebenbild – und doch voller Bosheit."),
    "fairy": ("Fey Elhai", "m", "Eine der edlen Waldfeen. Die Fey Elhai sind bekannt für ihren tiefen Hass auf ihre dunklen Brüder, die Fey Dirhai."),
    "flytrap": ("Menschenfresserpflanze", "f", "Eine wilde Pflanze mit einer Vorliebe für Menschenfleisch."),
    "frogman": ("Froschmann", "m", "Ein Froschmann, Angehöriger eines sanften Amphibienvolks. Sie hassen alle, die dem bösen Pfad folgen."),
    "fungusbeast": ("Pilzbestie", "f", "Dieses seltsame Wesen aus weichem, breiigem Fleisch bewegt sich langsam und bedächtig. Riesige Sporen haften an seinem Rücken."),
    "ghost": ("Geist", "m", "Lumpen und verfaultes Fleisch hängen an den Knochen dieser Erscheinung. Es ist der Geist Far'Nohls, eines einstigen Tyrannen von Meridian, der Leben und Seele den dunklen Künsten opferte."),
    "giantrat": ("Riesenratte", "f", "Die Riesenratte fletscht trotzig ihre gelben Zähne. Ihr verfilztes Fell stinkt nach Fäulnis."),
    "groundworm": ("Erdwurm", "m", "Ein furchterregendes, heimtückisches Wesen. Der Erdwurm lauert unter der Erde und schießt plötzlich hervor, um sein Opfer zu verschlingen."),
    "groundwormlarva": ("Erdwurmlarve", "f", "Die junge Form des Erdwurms, etwas schwächer als die Ausgewachsenen. Auch sie lauert unter der Erde und schießt plötzlich hervor."),
    "groundwormqueen": ("Erdwurmkönigin", "f", "Die größte der Erdwürmer. Ihr gewaltiger Hunger hat sie wahnsinnig gemacht; manchmal legt sie Eier in Leichen, aus denen Larven schlüpfen."),
    "guard": ("Wachkuh", "f", "Die bösartige Wachkuh, die alle Missetäter bestraft."),
    "iceperson": ("Eiswesen", "n", "Ein Geschöpf ganz aus gefrorenem Wasser. Raureif bedeckt die kristallene Gestalt, ihre gezackten Glieder schimmern im fahlen Licht."),
    "kriipa": ("Kriipa", "m", "Dieses spinnenbeinige Wesen huscht durchs Blätterdach des Dschungels. Reisende werden gewarnt: Die grinsenden Echsen köpfen einen Mann mit einem Hieb ihrer dolchscharfen Klauen."),
    "lich": ("Königin Venya'cyr", "f", "Eine edle Frau von atemberaubender Schönheit und vornehmer Haltung. Erstaunlich, wie sie ihr Antlitz all die Jahrhunderte so jung bewahrt hat."),
    "lupoggking": ("Lupogg-König", "m", "Der uralte König der Lupoggs herrscht über diese dunklen Höhlen. Die Felswände beben, wenn er brüllt."),
    "lupogg": ("Lupogg", "m", "Dieser Bewohner der Tiefe, hässlich und roh, soll friedlich sein – davon ist jetzt nichts zu merken. Die Lupoggs verteidigen ihr Revier."),
    "livingstatue": ("Statue", "f", "Diese Statue ist das Abbild eines uralten Kriegers."),
    "molluskmonster": ("Molluskenwesen", "n", "Ein riesiges, eher friedliches Geschöpf, das nach Nahrung sucht. Es greift selten an – doch sein dicker Panzer und seine kräftige Schere machen es zu einem harten Gegner."),
    "mummy": ("Mumie", "f", "Eine Mumie. In vergilbte Binden gewickelt schlurft sie dir entgegen."),
    "blackmummy": ("Schattenmumie", "f", "Eine Schattenmumie. Schwärzer als die Nacht und zäher als ihre Geschwister."),
    "mummynotreasure": ("Mumie", "f", "Eine Mumie. In vergilbte Binden gewickelt schlurft sie dir entgegen."),
    "narthylworm": ("Narthyl-Wurm", "m", "Trotz seiner fremdartigen Hässlichkeit sind die geschmeidigen Bewegungen des Narthyl-Wurms hypnotisch. Jeder Nerv schreit Gefahr, doch ein tranceartiger Zustand senkt sich über deinen Geist."),
    "orc": ("Ork", "m", "Dieser schmutzstarrende Diener Qors überragt jeden Menschen. In einer knorrigen Hand hält er eine Klinge."),
    "orcpitboss": ("Ork-Grubenboss", "m", "Um Grubenboss zu werden, musste dieser Ork über hundert seinesgleichen erschlagen. In den Höhlen gibt es keinen furchterregenderen Krieger."),
    "caveorc": ("Höhlenork", "m", "Dieser dreckige Ork streift hungrig durch die Höhlen, auf der Suche nach Edelsteinen und Futter."),
    "orcwizard": ("Ork-Zauberer", "m", "Die Ork-Zauberer schöpfen dunkle Kräfte aus der Tiefe und setzen den Willen ihres Häuptlings durch. Woher die Orks die Magie haben, weiß niemand."),
    "redant": ("Mutantenameise", "f", "Der Panzer der Mutantenameise ist dick und hart, ihre Zangen scharf und tödlich. Sie ist stärker und bösartiger als ihre dunkle Verwandte."),
    "reflection": ("Spiegelbild", "n", "Ein Spiegelbild, das sich gegen dich wendet."),
    "revenant": ("Wiedergänger", "m", "Die Strafe für eine Missetat: ein Kampf auf Leben und Tod mit einer verlorenen Seele. Er verfolgt nur den Mörder – wer sich einmischt, teilt dessen Schicksal."),
    "scorpion": ("Riesenskorpion", "m", "Der tödliche Stachel des Skorpions ragt hoch in die Luft, sein gepanzerter Leib schützt ihn vor allen außer den stärksten Hieben."),
    "shadowbeast": ("Schattenbestie", "f", "Eine magische Missgeburt aus Dschungel-Kriipa und einem halbstofflichen Wesen der Unterwelt. Wo sie ist, wird die Luft dunkler."),
    "skeleton": ("Skelett", "n", "Ein paar Fetzen verfaulten Fleisches hängen an diesen Knochen. Leblos, und doch von dunkler Magie bewegt."),
    "batteredskeleton": ("verwittertes Skelett", "n", "Menschliche Knochen unter einem verbeulten, eindeutig nichtmenschlichen Schädel. Zischende Flüssigkeit tropft von seinen Zähnen und frisst kleine Löcher in den Boden."),
    "daemonskeleton": ("Dämonenskelett", "n", "Schon der Anblick dieser widernatürlichen Kreatur lähmt manchen vor Schreck. Welches dunkle Ritual hat sie hervorgebracht?"),
    "tuskedskeleton": ("Hauerskelett", "n", "Menschliche Knochen unter einem Schädel mit großen Hauern. Welches dunkle Ritual hat diese Missgeburt hervorgebracht?"),
    "slime": ("Schleim", "m", "Eine zitternde Masse aus Glibber, die eine widerliche Spur hinterlässt. In ihrem durchsichtigen Inneren sind die Reste ihres letzten Opfers zu sehen."),
    "snowrat": ("Schneeratte", "f", "Die eisgeborene Verwandte des Festlandungeziefers – weniger ekelhaft, aber ein weit härterer Gegner."),
    "spiderbaby": ("Spinnenjunges", "n", "Kleiner und schwächer als seine großen Verwandten, aber trotzdem gefährlich. Sein weicher Bauch ist prall von der letzten Mahlzeit."),
    "spiderqueen": ("Spinnenkönigin", "f", "Der riesige Eiersack macht die Spinnenkönigin langsam und schwer, doch in ihren vielen roten Augen glimmt dunkle Klugheit."),
    "spectralmummy": ("Gespenstermumie", "f", "Eine Gespenstermumie. Halb durchsichtig schwebt sie zwischen den Gräbern."),
    "spider": ("Spinne", "f", "Dieses seltsam zarte Geschöpf bewegt sich lautlos und schlägt mit tödlicher Präzision zu."),
    "stonetroll": ("Steintroll", "m", "Dieser Steintroll bewacht das Herz von Zjiria und starrt jeden finster an. Lauf lieber."),
    "thrasher": ("Drescher", "m", "Ein massiges Ungetüm mit peitschenden Gliedern."),
    "troll": ("Troll", "m", "Mit knotigen Fleischwülsten bedeckt; der Troll hat mächtige Arme und rasiermesserscharfe Krallen."),
    "yeti": ("Yeti", "m", "Dieses prächtige weiße Tier überragt alles. Yetis sind äußerst gefährlich, vor allem in ihren Eishöhlen."),
    "zombie": ("Zombie", "m", "Ungezügeltes Böses hat diesen Toten wiederbelebt. Weißes, breiiges Fleisch klebt an weichen Knochen; die wässrigen Augen starren blind."),
}

# Abweichungen von den Klassenwerten (bisher im Code festgelegt bzw. Fehler im Original)
OVERRIDE = {
    # Iceperson: plResistances mit ATCK_SPELL_* ohne Minus – das Original wertet sie als Waffentypen. Übernommen wie
    # gemeint (Zaubertypen), siehe docs/monsters-original.md §9.
    "iceperson": {"resistancesFix": True},
    # Skelette lassen immer ein abgenutztes Langschwert fallen (Inventar im Constructor).
    "skeleton": {"alwaysDrops": "longsword"},
}

WEAPON = {"bludgeon", "thrust", "slash", "punch", "bite", "claw", "hit", "pierce", "sting", "whip", "magic", "nonmagic",
          "silver", "nerudite"}
DRAWFX = {"DRAWFX_BLACK": 0x00400000, "DRAWFX_TRANSLUCENT_50": 0x00200000, "DRAWFX_TRANSLUCENT_25": 0x00100000,
          "DRAWFX_TRANSLUCENT_75": 0x00300000, "DRAWFX_INVISIBLE": 0x00500000}


def type_key(name):
    """ATCK_WEAP_SLASH → slash, ATCK_SPELL_FIRE → fire, *_ALL → weapon_all/spell_all."""
    kind, t = re.match(r"ATCK_(WEAP|SPELL)_(\w+)", name).groups()
    t = t.lower()
    return ("weapon_all" if kind == "WEAP" else "spell_all") if t == "all" else t


def chance(text):
    m = re.match(r"\s*1\s*/\s*(\d+)", str(text))
    return int(m.group(1)) if m else None


def tid(sym):
    """TID_NEWBIE_AREA → newbie, TID_TOUGH → tough (Tabellen in data/items/items.json)."""
    if not sym or sym in ("TID_NONE", "0"):
        return None
    t = sym[4:].lower()
    return {"newbie_area": "newbie"}.get(t, t)


out = {}
for m in catalog:
    if m["category"] not in ("monster", "boss", "summoned"):
        continue
    key = m["class"].lower()
    name, gender, desc = TEXT[key]
    beh = set((m.get("behavior") or {}).get("flags", []))
    attrs = set((m.get("attributes") or {}).get("flags", []))
    entry = {
        "class": m["class"], "name": name, "gender": gender, "description": desc,
        "icon": m["icon"], "deadIcon": m.get("deadIcon"), "category": m["category"],
        "level": m["level"], "difficulty": m["difficulty"], "speed": m["speed"]["value"],
        "karma": m.get("karma") or 0, "wimpy": m.get("wimpyPercent") or 0,
        "treasure": tid((m.get("treasureType") or {}).get("symbolic")),
        "attack": [type_key(f) for f in (m.get("attackType") or {}).get("flags", [])],
        "attackSpell": [type_key(f) for f in (m.get("attackSpell") or {}).get("flags", [])],
        "behavior": sorted(f[3:].lower() for f in beh if f.startswith("AI_")),
        "attributes": sorted(f[4:].lower() for f in attrs if f.startswith("MOB_")),
        "bodyShape": m.get("bodyShape"), "size": m.get("size"),
        "undead": "undead" in m.get("tags", []),
    }
    res = {}
    for r in m.get("resistances") or []:
        for t in r["typeNames"]:
            k = type_key(t)
            if OVERRIDE.get(key, {}).get("resistancesFix") and r["kind"] == "weapon" and t.startswith("ATCK_SPELL"):
                k = k  # Zaubertyp wie gemeint
            res[k] = r["percent"]
    entry["resistances"] = res
    fx = (m.get("drawfx") or {}).get("name")
    if fx in DRAWFX:
        entry["drawFlags"] = DRAWFX[fx]
    sp = m.get("specials") or {}
    special = {}
    if "poison" in sp:
        p = sp["poison"]
        special["poison"] = {"chance": chance(p.get("chance")), "lossRate": p.get("lossRate"), "durationMs": p.get("durationMs")}
    for k in ("onHit",):
        if k in sp:
            special["onHit"] = sp[k]
    if "spellcasting" in sp:
        special["spellcasting"] = sp["spellcasting"]
    for k in ("ranged", "burrow", "darkness", "doubleAttack", "spin", "dormant", "magicResist", "fear", "drain"):
        if k in sp:
            special[k] = sp[k]
    if special:
        entry["special"] = special
    entry.update({k: v for k, v in OVERRIDE.get(key, {}).items() if k != "resistancesFix"})
    out[key] = entry

path = root / "server/data/monsters/monsters.json"
path.parent.mkdir(parents=True, exist_ok=True)
path.write_text(json.dumps({
    "_hinweis": "Erzeugt mit tools/make_monsters.py aus docs/kod/monsters_catalog.json. Werte der KOD-Klassen "
                "(monster.kod: LP, Treffer, Schaden, Takt aus Stufe/Schwierigkeit/Tempo), Texte eigene Übersetzung.",
    "monsters": out}, ensure_ascii=False, indent=2) + "\n")
print(f"{len(out)} Monster → {path}")
