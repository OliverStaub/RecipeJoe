# RecipeJoe

A personal, single-user recipe manager: import recipes from the web, browse them, and read them while cooking.

## Language

**Recipe**:
A single dish's record: title, servings, timing, an ordered list of Ingredient Lines, an ordered list of Steps, an optional image, and (if imported) a Source.
_Avoid_: Dish (a Recipe describes how to make a dish, it isn't the dish itself)

**Ingredient Line**:
One line of a Recipe's ingredient list, as authored (e.g. "2 cups flour, sifted") — free text, not decomposed into quantity/unit/food fields in V1.
_Avoid_: Ingredient (implies a standalone structured entity, which V1 doesn't have)

**Step**:
One instruction in a Recipe's ordered cooking procedure.
_Avoid_: Instruction (reserve for the Recipe's instruction list as a whole, if ever named)

**Servings**:
How much a Recipe makes, as authored — free text (e.g. "4 Portionen", "12 cookies", "2 potpies (8 servings each)"). Not a number in V1.
_Avoid_: Yield (schema.org's term; Servings is what a cook actually thinks in)

**Source**:
The URL a Recipe was Imported from. Recipes entered by hand have no Source.
_Avoid_: Origin, URL

**Import**:
Creating one or more Recipes from a Source URL. Comes in two kinds, Web Import and Video Import, which follow different paths but end the same way: Recipes in the Library. An Import runs in the background: it is Pending (at some stage) until it ends; on success it disappears, leaving its Recipes; on failure it stays Failed until retried or dismissed.
_Avoid_: Scrape, Ingest, Import Job

**Web Import**:
An Import from a web page, parsing its embedded schema.org/Recipe structured data. Only understands standard structured data — never bespoke per-site scraping. Produces exactly one Recipe.

**Video Import**:
An Import from a YouTube video, reading the video's available text (transcript, title, description) and thumbnail, and turning it into Recipes written as proper German recipes. One video may yield several Recipes.

**Recipe Draft**:
A Recipe that is not yet in the Library, e.g. what an Import produces. Saving it adds it to the Library. After a redirect, its Source is the page the Recipe was actually found on.
_Avoid_: Parsed recipe, Candidate

**Library**:
The full set of a user's saved Recipes — what the searchable list screen shows.
_Avoid_: Collection, Catalog

**New Recipe**:
A Recipe an Import added to the Library that hasn't been opened in Cook View yet. Opening it once makes it an ordinary Recipe.
_Avoid_: Unread, Unseen

**Cook View**:
The Recipe detail screen, laid out for reading a Recipe step-by-step while actively cooking.
_Avoid_: Detail page, Reader view
