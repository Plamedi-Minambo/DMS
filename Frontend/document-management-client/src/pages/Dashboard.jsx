
import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";

import { getUser, logout } from "../utils/auth";
import api from "../services/api";

import "./Dashboard.css";

function Dashboard() {
  const navigate = useNavigate();
  const user = getUser();

  const [documents, setDocuments] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let isMounted = true;

    const fetchDashboardData = async () => {
      try {
        setLoading(true);

        const response = await api.get("/Documents");

        if (isMounted) {
          setDocuments(response.data || []);
        }
      } catch (error) {
        console.error("Failed to load dashboard data:", error);

        if (error.response?.status === 401) {
          logout();
          navigate("/login");
        }
      } finally {
        if (isMounted) {
          setLoading(false);
        }
      }
    };

    fetchDashboardData();

    return () => {
      isMounted = false;
    };
  }, [navigate]);

  const handleLogout = () => {
    logout();
    navigate("/login");
  };

  const totalDocuments = documents.length;

  const pendingDocuments = documents.filter(
    (document) =>
      document.status === "Pending" ||
      document.status === "Pending Manager" ||
      document.status === "Pending Finance"
  ).length;

  const approvedDocuments = documents.filter(
    (document) => document.status === "Approved"
  ).length;

  const rejectedDocuments = documents.filter(
    (document) => document.status === "Rejected"
  ).length;

  return (
    <div className="dashboard-page">

      {/* Sidebar */}
      <aside className="dashboard-sidebar">

        <div className="sidebar-logo">
          <h2>DMS</h2>
          <p>Document Management</p>
        </div>

        <nav className="sidebar-navigation">

          <button
            className="nav-item active"
            onClick={() => navigate("/dashboard")}
          >
            🏠 Dashboard
          </button>

          <button
            className="nav-item"
            onClick={() => navigate("/documents")}
          >
            📄 Documents
          </button>

          <button
            className="nav-item"
            onClick={() => navigate("/reports")}
          >
            📊 Reports
          </button>

          {/* AI Insights */}
          <button
            className="nav-item"
            onClick={() => navigate("/ai-insights")}
          >
            🤖 AI Insights
          </button>

          <button
            className="nav-item"
            onClick={() => navigate("/approval-workflow")}
          >
            🔐 Approval Workflow
          </button>

        </nav>

        <button
          className="logout-button"
          onClick={handleLogout}
        >
          🚪 Logout
        </button>

      </aside>

      {/* Main Content */}
      <main className="dashboard-content">

        {/* Header */}
        <header className="dashboard-header">

          <div>
            <h1>Dashboard</h1>

            <p>
              Welcome back, {user?.fullName || "User"}!
            </p>
          </div>

          <div className="user-info">

            <div className="user-avatar">
              {(user?.fullName || "U")
                .charAt(0)
                .toUpperCase()}
            </div>

            <div>
              <strong>
                {user?.fullName || "User"}
              </strong>

              <span>
                {user?.role || "User"}
              </span>
            </div>

          </div>

        </header>

        {/* Statistics */}
        <section className="dashboard-cards">

          <div className="dashboard-card">

            <div className="card-icon">
              📄
            </div>

            <div>
              <h3>Total Documents</h3>

              <p>
                {loading ? "..." : totalDocuments}
              </p>
            </div>

          </div>

          <div className="dashboard-card">

            <div className="card-icon">
              ⏳
            </div>

            <div>
              <h3>Pending Approval</h3>

              <p>
                {loading ? "..." : pendingDocuments}
              </p>
            </div>

          </div>

          <div className="dashboard-card">

            <div className="card-icon">
              ✅
            </div>

            <div>
              <h3>Approved</h3>

              <p>
                {loading ? "..." : approvedDocuments}
              </p>
            </div>

          </div>

          <div className="dashboard-card">

            <div className="card-icon">
              ❌
            </div>

            <div>
              <h3>Rejected</h3>

              <p>
                {loading ? "..." : rejectedDocuments}
              </p>
            </div>

          </div>

        </section>

        {/* Welcome Section */}
        <section className="dashboard-welcome">

          <h2>Document Management System</h2>

          <p>
            Manage, review, approve and track your documents
            from one central location.
          </p>

          <div className="welcome-info">

            <div>
              <strong>Logged in as:</strong>

              <span>
                {user?.email || "Unknown"}
              </span>
            </div>

            <div>
              <strong>Role:</strong>

              <span>
                {user?.role || "Unknown"}
              </span>
            </div>

          </div>

        </section>

      </main>

    </div>
  );
}

export default Dashboard;

