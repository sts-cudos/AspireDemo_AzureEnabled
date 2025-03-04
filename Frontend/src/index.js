async function ping() {
    const response = await fetch(`${__SERVICE_BASE__}/ping`);
    const data = await response.text();
    document.getElementById('ping-result').innerText = data;
}

async function add() {
    const response = await fetch(`${__SERVICE_BASE__}/add`);
    const data = await response.json();
    document.getElementById('add-result').innerText = data.sum;
}

async function postgres() {
    const response = await fetch(`${__SERVICE_BASE__}/answer-from-db`);
    const data = await response.json();
    document.getElementById('postgres-result').innerText = data.answer;
}
