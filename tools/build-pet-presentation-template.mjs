// Regenerate the embedded two-slide template in the Notion skill environment.
// Usage: node tools/build-pet-presentation-template.mjs /tmp/template-dir
import { buildPptx, createDeck } from '/skills/pptx/scripts/pptx_builder_runtime.mjs';
import { applyRecipeDeckPlan } from '/skills/pptx/scripts/slide_recipes.mjs';
import { mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
const out = resolve(process.argv[2] || 'presentation-template-build');
await mkdir(out, { recursive: true });
const deck = createDeck();
applyRecipeDeckPlan(deck, [
  { recipeId: 'cover-editorial', content: { eyebrow: 'AI PRESENTATION', title: 'COVER_TITLE', subtitle: 'COVER_SUBTITLE' } },
  { recipeId: 'two-column', content: { title: 'SLIDE_TITLE', left: 'LEFT_CONTENT', right: 'RIGHT_CONTENT' } }
]);
await buildPptx(deck, { scenePath: `${out}/scene.json`, outputPath: `${out}/template.pptx`, reportPath: `${out}/report.json`, previewDir: `${out}/preview`, layoutDir: `${out}/layout` });
