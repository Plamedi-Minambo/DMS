import { Link } from "react-router-dom";

function Unauthorized() {
  return (
    <div
      style={{
        padding: "50px",
        textAlign: "center"
      }}
    >
      <h1>403</h1>

      <h2>Access Denied</h2>

      <p>
        You do not have permission to view this page.
      </p>

      <Link to="/dashboard">
        Return to Dashboard
      </Link>
    </div>
  );
}

export default Unauthorized;