import { useEffect, useRef, useState } from "react";
import DashboardLayout from "../components/DashboardLayout";
import { Briefcase, Building2, UserCircle } from "../components/ProfileIcons";
import { API_ORIGIN, apiFetch, throwIfNotOk } from "../api";
import { useMockAuth, avatarInitials } from "../context/MockAuthContext";
import { printStaffProfile } from "../print/printRecord";
import "./StudentRecords.css";
import "./ManagementProfile.css";
import "./StudentProfile.css";

function displayOrDash(value) {
  if (value == null || String(value).trim() === "") return "—";
  return value;
}

function roleLabel(me, staff) {
  return displayOrDash(staff?.roleSql || staff?.role || me?.roleSql || me?.role);
}

function emptyContact() {
  return {
    phoneNumber: "",
    personalEmail: "",
    postalAddress: "",
    province: "",
    district: "",
    village: "",
    emergencyContactName: "",
    emergencyContactPhone: "",
    emergencyRelationship: "",
  };
}

function contactFromStaff(staff) {
  return {
    phoneNumber: staff?.phoneNumber || "",
    personalEmail: staff?.personalEmail || "",
    postalAddress: staff?.postalAddress || "",
    province: staff?.province || "",
    district: staff?.district || "",
    village: staff?.village || "",
    emergencyContactName: staff?.emergencyContactName || "",
    emergencyContactPhone: staff?.emergencyContactPhone || "",
    emergencyRelationship: staff?.emergencyRelationship || "",
  };
}

export default function StaffSelfProfile({ title = "My Profile", navItems = [], fallbackPhoto = null }) {
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
  const [contactForm, setContactForm] = useState(emptyContact());
  const [saveError, setSaveError] = useState(null);
  const photoInputRef = useRef(null);

  const loadProfile = async () => {
    const res = await apiFetch(`${API_ORIGIN}/api/me`);
    await throwIfNotOk(res);
    const record = await res.json();
    setMe(record);

    const staffRes = await apiFetch(`${API_ORIGIN}/api/staff/me`);
    await throwIfNotOk(staffRes);
    const staffRecord = await staffRes.json();
    setStaff(staffRecord);
    setContactForm(contactFromStaff(staffRecord));
    return staffRecord;
  };

  useEffect(() => {
    (async () => {
      setIsLoading(true);
      try {
        await loadProfile();
        setApiError(null);
      } catch {
        setMe(null);
        setStaff(null);
        setApiError(
          "Couldn't load your profile. Make sure the API is running and you are signed in as staff."
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

  const handleChange = (e) => {
    setContactForm({ ...contactForm, [e.target.name]: e.target.value });
    setSaved(false);
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
      await loadProfile();
      setEditingContact(false);
      setSaved(true);
      if (refreshMe) await refreshMe();
    } catch (err) {
      setSaveError(err.message || "Couldn't save contact details.");
    } finally {
      setSaving(false);
    }
  };

  const handlePrint = async () => {
    if (!staff) return;
    await printStaffProfile(staff, displayName);
  };

  const photoSrc = pendingPreview || avatarUrl || fallbackPhoto;
  const initial = avatarInitials(staff?.fullName || me?.displayName || displayName);

  return (
    <DashboardLayout title={title} navItems={navItems}>
      {apiError && <div className="records-error">{apiError}</div>}
      {isLoading && (
        <p style={{ color: "var(--text-light)", marginBottom: 18 }}>
          Loading profile…
        </p>
      )}

      {!isLoading && me && staff && (
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
              <button type="button" className="profile-edit-btn" onClick={handlePrint}>
                Print
              </button>
            </div>
            {photoError && <div className="records-error">{photoError}</div>}
            {saved && !editingContact && <p className="profile-saved-note">✓ Details saved successfully.</p>}
            <p className="profile-avatar-caption">{displayOrDash(staff.fullName || me.displayName || me.email)}</p>
          </div>

          <div className="dash-card-grid">
            <section className="dash-card">
              <h3 className="profile-card-title">
                <UserCircle aria-hidden="true" />
                <span>Account Information</span>
              </h3>
              <dl className="profile-card-fields">
                <div>
                  <dt>Staff Number</dt>
                  <dd>{displayOrDash(staff.staffNumber)}</dd>
                </div>
                <div>
                  <dt>Portal Email</dt>
                  <dd>{displayOrDash(staff.email || me.email)}</dd>
                </div>
                <div>
                  <dt>Role</dt>
                  <dd>{roleLabel(me, staff)}</dd>
                </div>
                <div>
                  <dt>Status</dt>
                  <dd>{displayOrDash(staff.status)}</dd>
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
                  <dd>{displayOrDash(staff.jobTitle || me.jobTitle)}</dd>
                </div>
                <div>
                  <dt>Full Name</dt>
                  <dd>{displayOrDash(staff.fullName)}</dd>
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
                  <dd>{displayOrDash(staff.departmentName)}</dd>
                </div>
                <div>
                  <dt>Faculty</dt>
                  <dd>{displayOrDash(staff.facultyName)}</dd>
                </div>
              </dl>
            </section>
          </div>

          <section className="dash-card" style={{ marginTop: 18 }}>
            <h3 className="profile-card-title">
              <span>Personal &amp; Emergency Details</span>
            </h3>
            {saveError && <div className="records-error">{saveError}</div>}
            {!editingContact ? (
              <>
                <dl className="profile-card-fields">
                  <div>
                    <dt>Phone Number</dt>
                    <dd>{displayOrDash(staff.phoneNumber)}</dd>
                  </div>
                  <div>
                    <dt>Personal Email</dt>
                    <dd>{displayOrDash(staff.personalEmail)}</dd>
                  </div>
                  <div>
                    <dt>Postal Address</dt>
                    <dd>{displayOrDash(staff.postalAddress)}</dd>
                  </div>
                  <div>
                    <dt>Province</dt>
                    <dd>{displayOrDash(staff.province)}</dd>
                  </div>
                  <div>
                    <dt>District</dt>
                    <dd>{displayOrDash(staff.district)}</dd>
                  </div>
                  <div>
                    <dt>Village</dt>
                    <dd>{displayOrDash(staff.village)}</dd>
                  </div>
                  <div>
                    <dt>Emergency Contact Name</dt>
                    <dd>{displayOrDash(staff.emergencyContactName)}</dd>
                  </div>
                  <div>
                    <dt>Emergency Contact Phone</dt>
                    <dd>{displayOrDash(staff.emergencyContactPhone)}</dd>
                  </div>
                  <div>
                    <dt>Emergency Relationship</dt>
                    <dd>{displayOrDash(staff.emergencyRelationship)}</dd>
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
                  Phone Number
                  <input type="tel" name="phoneNumber" value={contactForm.phoneNumber} onChange={handleChange} />
                </label>
                <label>
                  Personal Email
                  <input type="email" name="personalEmail" value={contactForm.personalEmail} onChange={handleChange} />
                </label>
                <label className="profile-form-span">
                  Postal Address
                  <input type="text" name="postalAddress" value={contactForm.postalAddress} onChange={handleChange} />
                </label>
                <label>
                  Province
                  <input type="text" name="province" value={contactForm.province} onChange={handleChange} />
                </label>
                <label>
                  District
                  <input type="text" name="district" value={contactForm.district} onChange={handleChange} />
                </label>
                <label>
                  Village
                  <input type="text" name="village" value={contactForm.village} onChange={handleChange} />
                </label>
                <label>
                  Emergency Contact Name
                  <input type="text" name="emergencyContactName" value={contactForm.emergencyContactName} onChange={handleChange} />
                </label>
                <label>
                  Emergency Contact Phone
                  <input type="tel" name="emergencyContactPhone" value={contactForm.emergencyContactPhone} onChange={handleChange} />
                </label>
                <label>
                  Emergency Relationship
                  <input type="text" name="emergencyRelationship" value={contactForm.emergencyRelationship} onChange={handleChange} />
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
