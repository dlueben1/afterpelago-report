export default function AppBarLip() {
  return (
    <svg
      aria-hidden="true"
      class="pointer-events-none absolute -bottom-4.5 left-0 h-5 w-70"
      viewBox="0 0 280 20"
      preserveAspectRatio="none"
    >
      <defs>
        <linearGradient id="appbar-lip" x1="0" x2="1">
          <stop offset="0%" stop-color="#2f7bff" />
          <stop offset="100%" stop-color="#456df8" />
        </linearGradient>
      </defs>

      <path
        fill="url(#appbar-lip)"
        d="
          M0 0
          H280
          C230 3 190 8 140 13
          C90 18 38 20 0 12
          Z
        "
      />
    </svg>
  );
}
