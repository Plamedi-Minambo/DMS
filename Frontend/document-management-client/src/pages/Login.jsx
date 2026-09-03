
import { useState } from "react";
import { useNavigate } from "react-router-dom";

import api from "../services/api";
import { saveAuth } from "../utils/auth";

import "./Login.css";

function Login() {
  const navigate = useNavigate();

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");

  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  const handleLogin = async (event) => {
    event.preventDefault();

    setError("");
    setLoading(true);

    try {
      const response = await api.post("/auth/login", {
        email,
        password
      });

      console.log("Login response:", response.data);

      const data = response.data || {};

      const token = data.token || data.Token;
      const userId = data.userId || data.UserId;
      const fullName = data.fullName || data.FullName;
      const userEmail = data.email || data.Email;
      const role = data.role || data.Role;

      if (!token) {
        setError(
          data.message ||
          "Login failed. No token received from server."
        );
        return;
      }

      const user = {
        id: userId,
        fullName,
        email: userEmail,
        role
      };

      saveAuth(token, user);

      navigate("/dashboard");

    } catch (error) {
      console.error("Login error:", error);

      setError(
        `Status: ${error.response?.status || "Unknown"} - ${
          error.response?.data?.message || error.message
        }`
      );

    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="login-page">
      <div className="login-card">

        <h1>Document Management System</h1>

        <p className="login-subtitle">
          Sign in to your account
        </p>

        {error && (
          <div className="error-message">
            {error}
          </div>
        )}

        <form onSubmit={handleLogin}>

          <div className="form-group">
            <label>Email</label>

            <input
              type="email"
              placeholder="Enter your email"
              value={email}
              onChange={(event) =>
                setEmail(event.target.value)
              }
              required
            />
          </div>

          <div className="form-group">
            <label>Password</label>

            <input
              type="password"
              placeholder="Enter your password"
              value={password}
              onChange={(event) =>
                setPassword(event.target.value)
              }
              required
            />
          </div>

          <button
            type="submit"
            disabled={loading}
          >
            {loading ? "Signing in..." : "Login"}
          </button>

        </form>
      </div>
    </div>
  );
}

export default Login;
