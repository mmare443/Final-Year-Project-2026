import { useEffect, useRef, useState } from "react";
import DashboardLayout from "../components/DashboardLayout";
import { Briefcase, Building2, UserCircle } from "../components/ProfileIcons";
import { API_ORIGIN, apiFetch, throwIfNotOk } from "../api";
import { useMockAuth, avatarInitials } from "../context/MockAuthContext";
import { LECTURER_NAV } from "./Attendance";
import "./StudentRecords.css";
import "./ManagementProfile.css";
import "./StudentProfile.css";

function displayOrDash(value) {
  if (value == null || String(value).trim() === "") return "—";
  return value;
}

function roleLabel(me) {
  return displayOrDash(me.roleSql || me.role);
}

export default function LecturerProfile() {
  const { avatarUrl, uploadProfilePhoto, displayName, refreshMe } = useMockAuth();
  const [me, setMe] = useState(null);
  const [staff, setStaff] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [apiError, setApiError] = useState(null);
  const [photoError, setPhotoError] = useState(null);
  const [pendingPhoto, setPendingPhoto] = useState(null);
  const [pendingPreview, setPendingPreview] = useState(null);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [editingContact, setEditingContact] = useState(false);
  const [contactForm, setContactForm] = useState({ fullName: "", employmentDetails: "" });
  const [saveError, setSaveError] = useState(null);
  const photoInputRef = useRef(null);

  useEffect(() => {
    (async () => {
      setIsLoading(true);
      try {
        const res = await apiFetch(`${API_ORIGIN}/api/me`);
        if (!res.ok) throw new Error(`API returned ${res.status}`);
        const record = await res.json();
        setMe(record);

        if (record.staffId) {
          const staffRes = await apiFetch(`${API_ORIGIN}/api/staff/${record.staffId}`);
          if (staffRes.ok) {
            const staffRecord = await staffRes.json();
            setStaff(staffRecord);
            setContactForm({
              fullName: staffRecord.fullName || "",
              employmentDetails: staffRecord.employmentDetails || "",
            });
          }
        }

        setApiError(null);
      } catch {
        setMe(null);
        setStaff(null);
        setApiError(
          "Couldn't reach the backend API. Make sure `dotnet run` is running on http://localhost:5000."
        );
      } finally {
        setIsLoading(false);
      }
    })();
  }, []);

  useEffect(() => () => {
    if (pendingPreview) URL.revokeObjectURL(pendingPreview);
  }, [pendingPreview]);

  const pickPhoto = (file) => {
    if (!file) return;
    setPhotoError(null);
    if (!["image/jpeg", "image/png"].includes(file.type)) {
      setPhotoError("Please upload a JPG or PNG.");
      return;
    }
    if (file.size > 5 * 1024 * 1024) {
      setPhotoError("File too large — 5 MB maximum.");
      return;
    }
    if (pendingPreview) URL.revokeObjectURL(pendingPreview);
    setPendingPhoto(file);
    setPendingPreview(URL.createObjectURL(file));
    setSaved(false);
  };

  const handleSavePhoto = async () => {
    if (!pendingPhoto) return;
    setSaving(true);
    setPhotoError(null);
    try {
      await uploadProfilePhoto(pendingPhoto);
      if (pendingPreview) URL.revokeObjectURL(pendingPreview);
      setPendingPhoto(null);
      setPendingPreview(null);
      setSaved(true);
    } catch (err) {
      setPhotoError(err.message || "Couldn't save the profile photo.");
    } finally {
      setSaving(false);
    }
  };

  const handleSaveContact = async (e) => {
    e.preventDefault();
    setSaving(true);
    setSaveError(null);
    try {
      const res = await apiFetch(`${API_ORIGIN}/api/staff/me`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(contactForm),
      });
      await throwIfNotOk(res);
      const updated = await res.json();
      setStaff(updated);
      setEditingContact(false);
      setSaved(true);
      if (refreshMe) await refreshMe();
    } catch (err) {
      setSaveError(err.message || "Couldn't save contact details.");
    } finally {
      setSaving(false);
    }
  };

  const photoSrc = pendingPreview || avatarUrl;
  const initial = avatarInitials(staff?.fullName || me?.displayName || displayName);

  return (
    <DashboardLayout title="My Profile" navItems={LECTURER_NAV}>
      {apiError && <div className="records-error">{apiError}</div>}
      {isLoading && (
        <p style={{ color: "var(--text-light)", marginBottom: 18 }}>
          Loading profile…
        </p>
      )}

      {!isLoading && me && (
        <>
          <div className="profile-avatar">
            <button
              type="button"
              className="profile-photo-btn"
              onClick={() => photoInputRef.current?.click()}
              title="Select a new profile photo"
            >
              {photoSrc ? (
                <img src={photoSrc} alt="Profile" className="profile-photo-img" />
              ) : initial ? (
                <span className="profile-photo-initial">{initial}</span>
              ) : (
                <UserCircle className="profile-avatar-icon" />
              )}
            </button>
            <input
              ref={photoInputRef}
              type="file"
              accept="image/jpeg,image/png"
              onChange={(e) => pickPhoto(e.target.files[0])}
              className="profile-photo-input"
            />
            <p className="profile-photo-hint">Click the photo to choose a new image</p>
            <div className="profile-photo-actions">
              <button type="button" className="profile-photo-action" onClick={() => photoInputRef.current?.click()}>
                Change Photo
              </button>
              <button
                type="button"
                className="profile-save-btn"
                onClick={handleSavePhoto}
                disabled={saving || !pendingPhoto}
              >
                {saving && pendingPhoto ? "Saving…" : "Save Photo"}
              </button>
            </div>
            {photoError && <div className="records-error">{photoError}</div>}
            {saved && !editingContact && <p className="profile-saved-note">✓ Details saved successfully.</p>}
            <p className="profile-avatar-caption">{displayOrDash(staff?.fullName || me.displayName || me.email)}</p>
          </div>

          <div className="dash-card-grid">
            <section className="dash-card">
              <h3 className="profile-card-title">
                <UserCircle aria-hidden="true" />
                <span>Account Information</span>
              </h3>
              <dl className="profile-card-fields">
                <div>
                  <dt>Email</dt>
                  <dd>{displayOrDash(me.email)}</dd>
                </div>
                <div>
                  <dt>Role</dt>
                  <dd>{roleLabel(me)}</dd>
                </div>
              </dl>
            </section>

            <section className="dash-card">
              <h3 className="profile-card-title">
                <Briefcase aria-hidden="true" />
                <span>Employment Information</span>
              </h3>
              <dl className="profile-card-fields">
                <div>
                  <dt>Job Title</dt>
                  <dd>{displayOrDash(me.jobTitle || staff?.jobTitle)}</dd>
                </div>
                <div>
                  <dt>Staff ID</dt>
                  <dd>{displayOrDash(staff?.staffNumber || me.staffId)}</dd>
                </div>
              </dl>
            </section>

            <section className="dash-card">
              <h3 className="profile-card-title">
                <Building2 aria-hidden="true" />
                <span>Organisation Information</span>
              </h3>
              <dl className="profile-card-fields">
                <div>
                  <dt>Department</dt>
                  <dd>{displayOrDash(staff?.departmentName)}</dd>
                </div>
                <div>
                  <dt>Faculty</dt>
                  <dd>{displayOrDash(staff?.facultyName)}</dd>
                </div>
              </dl>
            </section>
          </div>

          <section className="dash-card" style={{ marginTop: 18 }}>
            <h3 className="profile-card-title">
              <span>Contact Details</span>
            </h3>
            {saveError && <div className="records-error">{saveError}</div>}
            {!editingContact ? (
              <>
                <dl className="profile-card-fields">
                  <div>
                    <dt>Full Name</dt>
                    <dd>{displayOrDash(staff?.fullName)}</dd>
                  </div>
                  <div>
                    <dt>Additional details</dt>
                    <dd>{displayOrDash(staff?.employmentDetails)}</dd>
                  </div>
                </dl>
                <div className="profile-save-row">
                  <button type="button" className="profile-edit-btn" onClick={() => { setEditingContact(true); setSaved(false); }}>
                    Edit Details
                  </button>
                </div>
              </>
            ) : (
              <form onSubmit={handleSaveContact} className="profile-form">
                <label>
                  Full Name
                  <input
                    type="text"
                    name="fullName"
                    value={contactForm.fullName}
                    onChange={(e) => setContactForm({ ...contactForm, fullName: e.target.value })}
                    required
                  />
                </label>
                <label>
                  Additional details
                  <input
                    type="text"
                    name="employmentDetails"
                    value={contactForm.employmentDetails}
                    onChange={(e) => setContactForm({ ...contactForm, employmentDetails: e.target.value })}
                  />
                </label>
                <div className="profile-save-row">
                  <button type="submit" className="profile-save-btn" disabled={saving}>
                    {saving && !pendingPhoto ? "Saving…" : "Save"}
                  </button>
                  {saved && <span className="profile-saved-note">✓ Details saved successfully.</span>}
                </div>
              </form>
            )}
          </section>
        </>
      )}
    </DashboardLayout>
  );
}
