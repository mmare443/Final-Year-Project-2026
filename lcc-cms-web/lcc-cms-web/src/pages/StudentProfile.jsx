import { useEffect, useRef, useState } from "react";
import DashboardLayout from "../components/DashboardLayout";
import { useStudents } from "../context/StudentsContext";
import { useMockAuth, avatarInitials } from "../context/MockAuthContext";
import { STUDENT_NAV } from "./Attendance";
import "./StudentProfile.css";

export default function StudentProfile() {
  const { myProfile, isLoading, apiError, fetchMyProfile, updateMyProfile } = useStudents();
  const { avatarUrl, uploadProfilePhoto, displayName } = useMockAuth();
  const [form, setForm] = useState(null);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState(null);
  const [saved, setSaved] = useState(false);
  const [photoError, setPhotoError] = useState(null);
  const [pendingPhoto, setPendingPhoto] = useState(null);
  const [pendingPreview, setPendingPreview] = useState(null);
  const [editingContact, setEditingContact] = useState(false);
  const photoInputRef = useRef(null);

  useEffect(() => {
    fetchMyProfile();
  }, [fetchMyProfile]);

  useEffect(() => {
    if (myProfile) {
      setForm({
        phone: myProfile.phone,
        emergencyContactName: myProfile.emergencyContactName,
        emergencyContactPhone: myProfile.emergencyContactPhone,
        postalAddress: myProfile.postalAddress,
      });
    }
  }, [myProfile]);

  useEffect(() => () => {
    if (pendingPreview) URL.revokeObjectURL(pendingPreview);
  }, [pendingPreview]);

  const handleChange = (e) => {
    setForm({ ...form, [e.target.name]: e.target.value });
    setSaved(false);
  };

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
      setPhotoError(err.message || "Couldn't save the photo.");
    } finally {
      setSaving(false);
    }
  };

  const handleSave = async (e) => {
    e.preventDefault();
    setSaving(true);
    setSaveError(null);
    try {
      await updateMyProfile(form);
      await fetchMyProfile();
      setSaved(true);
      setEditingContact(false);
    } catch (err) {
      setSaveError(err.message || "Couldn't save — check the backend API is running.");
    } finally {
      setSaving(false);
    }
  };

  if (apiError) {
    return (
      <DashboardLayout title="My Profile" navItems={STUDENT_NAV}>
        <div className="profile-error">{apiError}</div>
      </DashboardLayout>
    );
  }

  if (isLoading || !myProfile || !form) {
    return (
      <DashboardLayout title="My Profile" navItems={STUDENT_NAV}>
        <p style={{ color: "var(--text-light)" }}>Loading your profile…</p>
      </DashboardLayout>
    );
  }

  const photoSrc = pendingPreview || avatarUrl;
  const initial = avatarInitials(myProfile.fullName || displayName);

  return (
    <DashboardLayout title="My Profile" navItems={STUDENT_NAV}>
      <div className="profile-layout">
        <div className="profile-photo-card">
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
              <span className="profile-photo-placeholder">＋<br />Add Photo</span>
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
          {photoError && <div className="field-note field-error">{photoError}</div>}
          <div className="profile-id-name">{myProfile.fullName}</div>
          <div className="profile-id-number">{myProfile.id}</div>
        </div>

        <div className="profile-details-card">
          <h2 className="profile-section-heading">Academic Information</h2>
          <div className="profile-readonly-grid">
            <div>
              <span className="profile-readonly-label">Programme</span>
              <span className="profile-readonly-value">{myProfile.programme}</span>
            </div>
            <div>
              <span className="profile-readonly-label">Year Level</span>
              <span className="profile-readonly-value">{myProfile.yearLevel ? `Year ${myProfile.yearLevel}` : "—"}</span>
            </div>
            <div>
              <span className="profile-readonly-label">Email</span>
              <span className="profile-readonly-value">{myProfile.email}</span>
            </div>
            <div>
              <span className="profile-readonly-label">Date of Birth</span>
              <span className="profile-readonly-value">{myProfile.dateOfBirth || "—"}</span>
            </div>
            <div>
              <span className="profile-readonly-label">Province / District / Village</span>
              <span className="profile-readonly-value">
                {[myProfile.province, myProfile.district, myProfile.village].filter(Boolean).join(" / ") || "—"}
              </span>
            </div>
          </div>
          <p className="profile-readonly-note">
            Academic info above is read-only here — corrections go through
            Registrar/Admin. Editable fields below are yours to maintain.
          </p>

          <h2 className="profile-section-heading">Contact &amp; Emergency Details</h2>
          {saveError && <div className="profile-error">{saveError}</div>}
          {!editingContact ? (
            <>
              <div className="profile-readonly-grid">
                <div>
                  <span className="profile-readonly-label">Phone Number</span>
                  <span className="profile-readonly-value">{form.phone || "—"}</span>
                </div>
                <div>
                  <span className="profile-readonly-label">Postal Address</span>
                  <span className="profile-readonly-value">{form.postalAddress || "—"}</span>
                </div>
                <div>
                  <span className="profile-readonly-label">Emergency Contact Name</span>
                  <span className="profile-readonly-value">{form.emergencyContactName || "—"}</span>
                </div>
                <div>
                  <span className="profile-readonly-label">Emergency Contact Phone</span>
                  <span className="profile-readonly-value">{form.emergencyContactPhone || "—"}</span>
                </div>
              </div>
              <div className="profile-save-row">
                <button type="button" className="profile-edit-btn" onClick={() => { setEditingContact(true); setSaved(false); }}>
                  Edit Details
                </button>
                {saved && <span className="profile-saved-note">✓ Details saved successfully.</span>}
              </div>
            </>
          ) : (
            <form onSubmit={handleSave} className="profile-form">
              <label>
                Phone Number
                <input type="tel" name="phone" value={form.phone || ""} onChange={handleChange} />
              </label>
              <label>
                Postal Address
                <input type="text" name="postalAddress" value={form.postalAddress || ""} onChange={handleChange} />
              </label>
              <label>
                Emergency Contact Name
                <input
                  type="text" name="emergencyContactName"
                  value={form.emergencyContactName || ""} onChange={handleChange}
                />
              </label>
              <label>
                Emergency Contact Phone
                <input
                  type="tel" name="emergencyContactPhone"
                  value={form.emergencyContactPhone || ""} onChange={handleChange}
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
        </div>
      </div>
    </DashboardLayout>
  );
}
