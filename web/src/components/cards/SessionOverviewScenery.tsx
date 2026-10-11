/** Decorative palm island silhouette, drawn in the current theme color. */
export default function SessionOverviewScenery() {
  return (
    <svg
      viewBox="0 0 320 260"
      fill="none"
      class="absolute -right-8 top-2 h-64 w-72 text-ap-blue opacity-20 sm:-right-2"
      aria-hidden="true"
    >
      {/* Water, reflections, and distant islands. */}
      <g fill="currentColor" opacity="0.45">
        <ellipse cx="190" cy="212" rx="114" ry="7" />
        <ellipse cx="201" cy="226" rx="78" ry="4" />
        <ellipse cx="175" cy="237" rx="44" ry="3" />
        <path d="M37 191c15-5 18-17 29-19 12-2 12-13 23-15 17-2 29 21 45 26l-97 8ZM230 167c8-12 12-27 25-30 14-3 21 14 22 22 9 1 18 7 25 12l-72-4Z" />
        <path d="m65 204 15-2 43 2-22 2-36-2Zm169 37 14-2 24 2-18 2-20-2Z" />
      </g>
      {/* The island and its softly raised shoreline. */}
      <g fill="currentColor">
        <path d="M84 197c15-8 33-9 48-17 14-8 16-23 34-28 26-8 48 1 60 21 10 17 31 13 44 24-41 16-144 16-186 0Z" />
        <path d="M64 202c49 20 175 23 224-1-12 14-46 21-109 21-53 0-101-8-115-20Z" opacity="0.55" />
        {/* Curved main trunk and coconuts. */}
        <path d="M181 175c9-23 22-58 19-102l12-4c6 47-1 82-12 111l-19-5Z" />
        <circle cx="204" cy="76" r="7" />
        <circle cx="214" cy="78" r="6" />
        {/* Broad, drooping palm fronds with cut leaf tips. */}
        <path d="M208 72c-8-33-27-43-44-28-4 4-6 11-7 17l13-8 1 9 14-5 23 15Z" />
        <path d="M208 72c4-32 23-44 41-31 5 4 8 11 9 17l-12-6-3 10-12-4-23 14Z" />
        <path d="M205 73c-33-18-61-8-69 22-2 8-1 15 0 20l11-15 5 9 8-19 7 7 10-16 28-8Z" />
        <path d="M210 72c34-15 61-2 69 23 3 9 3 17 2 23l-11-15-5 8-10-17-6 7-13-18-26-11Z" />
        <path d="M204 73c-21 0-36 24-33 52l10-11 4 9 6-21 8 5 5-34Z" />
        <path d="M212 73c21 10 32 33 27 58l-8-15-7 7-2-23-8 5-2-32Z" />
        {/* Two smaller palms, leaning toward the water. */}
        <path d="M135 185c-1-18-5-35-14-54l7-4c12 20 17 37 19 56l-12 2Z" />
        <path d="M127 132c-6-17-18-24-29-15l-5 11 11-5 3 7 9-5 11 7Zm0 0c4-18 15-26 27-17l5 10-12-3-3 7-8-2-9 5Z" />
        <path d="M126 132c-17-6-29 2-34 20l10-7 3 6 7-11 14-8Zm2 0c19-3 30 6 32 22l-10-8-5 6-6-12-11-8Z" />
        <path d="M99 184c1-12 1-24-3-37l6-2c6 14 8 26 6 40l-9-1Z" />
        <path d="M100 148c-7-15-16-17-24-9l-3 7 10-3 2 5 6-4 9 4Zm0 0c3-15 12-20 20-12l4 7-10-2-2 6-5-2-7 3Z" />
        <path d="M98 149c-12-3-20 5-20 17l7-6 3 4 4-9 6-6Zm4 0c12-1 19 6 20 16l-6-5-4 4-4-10-6-5Z" />
      </g>
    </svg>
  );
}
