// HTTP client for the Nexus ASP.NET Core API.
// Every call sends the JWT from the auth store; a 401 signs the user out.
import { useAuthStore } from "@/store/useAuthStore";

export const API_URL = (process.env.NEXT_PUBLIC_API_URL || "http://localhost:5080").replace(/\/$/, "");
// export const API_URL = (process.env.NEXT_PUBLIC_API_URL || "www.ugc-student-api.website-link.info").replace(/\/$/, "");

export class ApiError extends Error {
  constructor(message, status, body) {
    super(message);
    this.status = status;
    this.body = body;
    this.code = body?.code;
    this.data = body?.data;
  }
}

/** Sends the request; a FormData body goes as multipart (the browser sets the boundary), anything else as JSON. */
async function send(method, path, body, { auth = true, accept = "application/json" } = {}) {
  const headers = { Accept: accept };
  const form = typeof FormData !== "undefined" && body instanceof FormData;
  if (body !== undefined && !form) headers["Content-Type"] = "application/json";
  const token = useAuthStore.getState().token;
  if (auth && token) headers.Authorization = `Bearer ${token}`;

  let res;
  try {
    res = await fetch(`${API_URL}/api/v1${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : form ? body : JSON.stringify(body),
    });
  } catch {
    throw new ApiError(`Cannot reach the API at ${API_URL}. Is the backend running?`, 0, null);
  }

  if (!res.ok) {
    const json = await readJson(res);
    if (res.status === 401 && auth) useAuthStore.getState().clear();
    const message =
      json?.detail ||
      (res.status === 403
        ? "You do not have permission to do this."
        : res.status === 401
          ? "Your session has ended. Sign in again."
          : `Request failed (${res.status}).`);
    throw new ApiError(message, res.status, json);
  }
  return res;
}

async function readJson(res) {
  const text = await res.text();
  try {
    return text ? JSON.parse(text) : null;
  } catch {
    return null;
  }
}

async function request(method, path, body, options) {
  const res = await send(method, path, body, options);
  return res.status === 204 ? null : readJson(res);
}

/** A file the API streams (documents are private, so they are fetched with the token, not linked). */
async function download(path) {
  const res = await send("GET", path, undefined, { accept: "*/*" });
  return res.blob();
}

export const api = {
  get: (path) => request("GET", path),
  post: (path, body = {}) => request("POST", path, body),
  put: (path, body = {}) => request("PUT", path, body),
  delete: (path) => request("DELETE", path),
  /** POST multipart/form-data. */
  upload: (path, formData) => request("POST", path, formData),
  /** GET a file as a Blob. */
  download,
  login: (email, password) => request("POST", "/auth/login", { email, password }, { auth: false }),
  register: (body) => request("POST", "/auth/register", body, { auth: false }),
  forgotPassword: (email) => request("POST", "/auth/forgot-password", { email }, { auth: false }),
  resetPassword: (body) => request("POST", "/auth/reset-password", body, { auth: false }),
};

/** Builds "?a=1&b=2", skipping empty values and "all". */
export function qs(params) {
  const p = new URLSearchParams();
  Object.entries(params || {}).forEach(([k, v]) => {
    if (v !== undefined && v !== null && v !== "" && v !== "all") p.set(k, String(v));
  });
  const s = p.toString();
  return s ? `?${s}` : "";
}
