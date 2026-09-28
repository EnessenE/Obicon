// API Configuration
const API_BASE_URL = 'http://localhost:5000';
const API_AUTH_TOKEN = 'uwu';

// Helper for API calls
async function apiCall(method, endpoint, body = null) {
    const headers = {
        'Authorization': API_AUTH_TOKEN,
        'Content-Type': 'application/json'
    };
    
    const options = {
        method,
        headers
    };
    
    if (body) {
        options.body = JSON.stringify(body);
    }
    
    try {
        const response = await fetch(`${API_BASE_URL}${endpoint}`, options);
        if (!response.ok) {
            const error = await response.text();
            throw new Error(error || `HTTP ${response.status}`);
        }
        return response.json();
    } catch (error) {
        console.error('API call failed:', error);
        throw error;
    }
}

// Vue App
const { createApp, ref, watch } = Vue;

createApp({
    setup() {
        const show = ref('home');
        
        // State
        const nodes = ref([]);
        const tests = ref([]);
        const health = ref(null);
        
        const newNodeName = ref('');
        const newTest = ref({
            name: '',
            type: 'Ping',
            frequency: 'OneMinute',
            isActive: true,
            nodeIds: []
        });
        
        // Loading states
        const loading = ref({
            nodes: false,
            tests: false,
            health: false
        });
        
        // Error states
        const error = ref({
            nodes: null,
            tests: null,
            health: null
        });
        
        // Methods
        async function loadNodes() {
            loading.value.nodes = true;
            error.value.nodes = null;
            try {
                nodes.value = await apiCall('GET', '/v1/nodes');
            } catch (e) {
                error.value.nodes = e.message;
            } finally {
                loading.value.nodes = false;
            }
        }
        
        async function createNode() {
            if (!newNodeName.value.trim()) return;
            try {
                await apiCall('POST', '/v1/nodes', { name: newNodeName.value.trim() });
                newNodeName.value = '';
                await loadNodes();
            } catch (e) {
                error.value.nodes = e.message;
            }
        }
        
        async function deleteNode(id) {
            try {
                await apiCall('DELETE', `/v1/nodes/${id}`);
                await loadNodes();
            } catch (e) {
                error.value.nodes = e.message;
            }
        }
        
        async function loadTests() {
            loading.value.tests = true;
            error.value.tests = null;
            try {
                tests.value = await apiCall('GET', '/v1/tests');
            } catch (e) {
                error.value.tests = e.message;
            } finally {
                loading.value.tests = false;
            }
        }
        
        async function createTest() {
            try {
                await apiCall('POST', '/v1/tests', {
                    name: newTest.value.name,
                    type: parseInt(newTest.value.type),
                    nodeIds: [],
                    frequency: parseInt(newTest.value.frequency),
                    isActive: newTest.value.isActive
                });
                newTest.value = { name: '', type: 'Ping', frequency: 'OneMinute', isActive: true, nodeIds: [] };
                await loadTests();
            } catch (e) {
                error.value.tests = e.message;
            }
        }
        
        async function deleteTest(id) {
            try {
                await apiCall('DELETE', `/v1/tests/${id}`);
                await loadTests();
            } catch (e) {
                error.value.tests = e.message;
            }
        }
        
        async function triggerRun(testId) {
            try {
                await apiCall('POST', `/v1/tests/${testId}/run`);
            } catch (e) {
                error.value.tests = e.message;
            }
        }
        
        async function checkHealth() {
            show.value = 'health';
            loading.value.health = true;
            error.value.health = null;
            try {
                health.value = await apiCall('GET', '/v1/health');
            } catch (e) {
                error.value.health = e.message;
            } finally {
                loading.value.health = false;
            }
        }
        
        // Auto-load on view change
        const routeMap = {
            nodes: loadNodes,
            tests: loadTests
        };
        
        watch(() => show.value, (newVal) => {
            if (routeMap[newVal]) {
                routeMap[newVal]();
            }
        });
        
        return {
            show,
            nodes,
            tests,
            health,
            newNodeName,
            newTest,
            loading,
            error,
            createNode,
            deleteNode,
            createTest,
            deleteTest,
            triggerRun,
            checkHealth
        };
    }
}).mount('#app');
