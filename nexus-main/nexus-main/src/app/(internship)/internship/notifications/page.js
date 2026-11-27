import { redirect } from "next/navigation";

/** Notifications moved to the account section. */
export default function OldNotificationsPage() {
  redirect("/account/notifications");
}
