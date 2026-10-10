import { useEffect, useState } from "react";
import { ApiError } from "../../../api/client";
import { ticketsApi } from "../../../api/tickets";
import type {
  PagedResult,
  TicketFilters,
  TicketListItem,
} from "../../../types/api";

const DEBOUNCE_MS = 300;

export const emptyFilters: TicketFilters = {
  search: "",
  status: undefined,
  priority: undefined,
  categoryId: undefined,
  customerId: undefined,
  assignedAgentId: undefined,
  unassignedOnly: false,
  page: 1,
  pageSize: 20,
  sortBy: "createdAtUtc",
  sortDirection: "desc",
};

interface State {
  isLoading: boolean;
  data?: PagedResult<TicketListItem>;
  error?: string;
}

/** Owns the list filters and debounces only the search text. */
export function useTicketList() {
  const [search, setSearch] = useState(emptyFilters.search);
  const [queryFilters, setQueryFilters] = useState<TicketFilters>(emptyFilters);
  const [reloadVersion, setReloadVersion] = useState(0);
  const [state, setState] = useState<State>({ isLoading: true });

  useEffect(() => {
    if (search === queryFilters.search) {
      return;
    }

    const handle = window.setTimeout(() => {
      setQueryFilters((current) => ({
        ...current,
        search,
        page: 1,
      }));
    }, DEBOUNCE_MS);

    return () => window.clearTimeout(handle);
  }, [search, queryFilters.search]);

  useEffect(() => {
    let active = true;

    const load = async () => {
      setState((current) => ({
        ...current,
        isLoading: true,
        error: undefined,
      }));

      try {
        const data = await ticketsApi.getTickets(queryFilters);

        if (active) {
          setState({ isLoading: false, data });
        }
      } catch (error) {
        if (active) {
          const message =
            error instanceof ApiError
              ? error.message
              : "Unable to load tickets.";

          setState((current) => ({
            ...current,
            isLoading: false,
            error: message,
          }));
        }
      }
    };

    void load();

    return () => {
      active = false;
    };
  }, [queryFilters, reloadVersion]);

  const updateFilters = (patch: Partial<TicketFilters>) => {
    const { search: nextSearch, ...immediatePatch } = patch;

    if (nextSearch !== undefined) {
      setSearch(nextSearch);
    }

    if (Object.keys(immediatePatch).length > 0) {
      setQueryFilters((current) => ({
        ...current,
        ...immediatePatch,
        page: patch.page ?? 1,
      }));
    }
  };

  const resetFilters = () => {
    setSearch(emptyFilters.search);
    setQueryFilters(emptyFilters);
  };

  const reload = () => setReloadVersion((current) => current + 1);

  return {
    filters: { ...queryFilters, search },
    updateFilters,
    resetFilters,
    reload,
    ...state,
  };
}
