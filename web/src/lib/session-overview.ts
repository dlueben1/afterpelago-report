import type { SessionSummaryResponse } from "../api/generated/model/sessionSummaryResponse";

export type SessionSortColumn = "isFavorite" | "name" | "status" | "slotCount" | "startUtc" | "endUtc";
export type SessionSort = { column: SessionSortColumn; direction: "asc" | "desc" };

export function formatSessionDate(value: string): string {
  return new Date(value).toLocaleDateString(undefined, { month: "short", day: "numeric", year: "numeric" });
}

export function formatSessionTime(value: string): string {
  return new Date(value).toLocaleTimeString(undefined, { hour: "numeric", minute: "2-digit" });
}

function searchableDate(value: string): string {
  const date = new Date(value);
  const localIsoDate = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
  return `${value} ${localIsoDate} ${formatSessionDate(value)} ${formatSessionTime(value)} ${date.toLocaleDateString()}`;
}

/** Filters and sorts a copy, leaving the shared query cache untouched. */
export function selectSessions(sessions: readonly SessionSummaryResponse[], search: string, favoritesOnly: boolean, sort: SessionSort): SessionSummaryResponse[] {
  const needle = search.trim().toLocaleLowerCase();
  const filtered = sessions.filter((session) => {
    if (favoritesOnly && !session.isFavorite) return false;
    if (!needle) return true;
    const haystack = `${session.name} ${session.gameNames.join(" ")} ${searchableDate(session.startUtc)} ${searchableDate(session.endUtc)}`;
    return haystack.toLocaleLowerCase().includes(needle);
  });
  const direction = sort.direction === "asc" ? 1 : -1;
  return filtered.sort((a, b) => {
    let comparison: number;
    switch (sort.column) {
      case "name": case "status": comparison = a[sort.column].localeCompare(b[sort.column]); break;
      case "isFavorite": comparison = Number(a.isFavorite) - Number(b.isFavorite); break;
      case "slotCount": comparison = Number(a.slotCount) - Number(b.slotCount); break;
      default: comparison = Date.parse(a[sort.column]) - Date.parse(b[sort.column]);
    }
    return comparison * direction || Number(b.id) - Number(a.id);
  });
}
