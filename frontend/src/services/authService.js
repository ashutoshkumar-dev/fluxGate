import axios from 'axios'

// Dedicated Axios instance for Auth Service (http://localhost:5100)
// Intentionally separate from api.js to avoid circular dependency with useAuthStore.
const authApi = axios.create({
  baseURL: 'http://localhost:5100',
})

export default authApi
