import { useSyncExternalStore } from "react";
import { getAuthSession, subscribeToAuthSession } from "./authSession";

export function useAuthSession() {
  return useSyncExternalStore(
    subscribeToAuthSession,
    getAuthSession,
    getAuthSession,
  );
}
