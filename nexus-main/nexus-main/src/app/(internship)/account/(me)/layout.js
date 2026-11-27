import { ProfileHeader } from "./_components/ProfileHeader";

/** "My profile": one sidebar entry, with the person's details and documents as tabs. Each tab keeps its own address. */
export default function MyProfileLayout({ children }) {
  return (
    <div className="space-y-4">
      <ProfileHeader />
      {children}
    </div>
  );
}
