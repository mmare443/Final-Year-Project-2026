import { useEffect, useRef, useState } from "react";
import DashboardLayout from "../components/DashboardLayout";
import { useStudents } from "../context/StudentsContext";
import { API_ORIGIN } from "../context/MockDataContext";
import "./StudentProfile.css";

const NAV = [
  { label: "Overview", path: "/student" },
  "My Courses",
  { label: "Attendance", path: "/student/attendance" },
  { label: "Assignments", path: "/student/assignments" },
  "Results",
  { label: "Profile", path: "/student/profile" },
];

export default function StudentProfile() {
  const { myProfile, isLoading, apiError, fetchMyProfile, updateMyProfile, uploadMyPhoto } = useStudents();
  const [form, setForm] = useState(null);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState(null);
  const [saved, setSaved] = useState(false);
  const [photoError, setPhotoError] = useState(null);
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

  const handleChange = (e) => {
    setForm({ ...form, [e.target.name]: e.target.value });
    setSaved(false);
  };

  const handleSave = async (e) => {
    e.preventDefault();
    setSaving(true);
    setSaveError(null);
    try {
      await updateMyProfile(form);
      setSaved(true);
    } catch (err) {
      setSaveError(err.message || "Couldn't save — check the backend API is running.");
    } finally {
      setSaving(false);
    }
  };

  const handlePhotoClick = () => photoInputRef.current?.click();

  const handlePhotoChange = async (e) => {
    const file = e.target.files[0];
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

    try {
      await uploadMyPhoto(file);
    } catch (err) {
      setPhotoError(err.message || "Upload failed — check the backend API is running.");
    }
  };

  if (apiError) {
    return (
      <DashboardLayout title="My Profile" navItems={NAV}>
        <div className="profile-error">{apiError}</div>
      </DashboardLayout>
    );
  }

  if (isLoading || !myProfile || !form) {
    return (
      <DashboardLayout title="My Profile" navItems={NAV}>
        <p style={{ color: "var(--text-light)" }}>Loading your profile…</p>
      </DashboardLayout>
    );
  }

  return (
    <DashboardLayout title="My Profile" navItems={NAV}>
      <div className="profile-layout">
        <div className="profile-photo-card">
          <button type="button" className="profile-photo-btn" onClick={handlePhotoClick}>
            {myProfile.photoPath ? (
              <img src={`${API_ORIGIN}${myProfile.photoPath}`} alt="Profile" className="profile-photo-img" />
            ) : (
              <span className="profile-photo-placeholder">＋<br />Add Photo</span>
            )}
          </button>
          <input
            ref={photoInputRef}
            type="file"
            accept="image/jpeg,image/png"
            onChange={handlePhotoChange}
            className="profile-photo-input"
          />
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
          <form onSubmit={handleSave} className="profile-form">
            <label>
              Phone Number
              <input type="tel" name="phone" value={form.phone} onChange={handleChange} />
            </label>
            <label>
              Postal Address
              <input type="text" name="postalAddress" value={form.postalAddress} onChange={handleChange} />
            </label>
            <label>
              Emergency Contact Name
              <input
                type="text" name="emergencyContactName"
                value={form.emergencyContactName} onChange={handleChange}
              />
            </label>
            <label>
              Emergency Contact Phone
              <input
                type="tel" name="emergencyContactPhone"
                value={form.emergencyContactPhone} onChange={handleChange}
              />
            </label>

            <div className="profile-save-row">
              <button type="submit" className="profile-save-btn" disabled={saving}>
                {saving ? "Saving…" : "Save Changes"}
              </button>
              {saved && <span className="profile-saved-note">Saved ✓</span>}
            </div>
          </form>
        </div>
      </div>
    </DashboardLayout>
  );
}
