# Avatares SGOL

Run `npm ci` and `npm run build` here after editing `avatars.jsx`.
Commit the generated `wwwroot/js/person-avatars*` assets along with the lockfile.
Node is required only to rebuild assets, never by the SGOL host.

Uses the official MIT-licensed `boring-avatars` package, with Preact's React
compatibility adapter. Avatars are generated locally from the stable person ID
and the saved seed, variant and palette. No names or profile data are sent to
an avatar API. Each generated SVG is displayed as an image to isolate SVG IDs.
