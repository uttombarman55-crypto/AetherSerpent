export function getDashboardHtml(): string {
  return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>AetherSerpent: Eternal Coil — Celestial Edge Network</title>
  <link rel="preconnect" href="https://fonts.googleapis.com">
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
  <link href="https://fonts.googleapis.com/css2?family=Cinzel:wght@600;800;900&family=Inter:wght@300;400;600;700&family=JetBrains+Mono:wght@400;600&display=swap" rel="stylesheet">
  <style>
    :root {
      --bg: #07090e;
      --card-bg: rgba(16, 22, 34, 0.75);
      --card-border: rgba(64, 224, 208, 0.18);
      --cyan: #40e0d0;
      --cyan-glow: rgba(64, 224, 208, 0.35);
      --gold: #ffd700;
      --gold-glow: rgba(255, 215, 0, 0.3);
      --green: #40ff80;
      --text: #e2e8f0;
      --text-muted: #94a3b8;
    }
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body {
      background-color: var(--bg);
      background-image: 
        radial-gradient(circle at 50% 0%, rgba(64, 224, 208, 0.12), transparent 50%),
        radial-gradient(circle at 80% 60%, rgba(255, 215, 0, 0.06), transparent 45%),
        radial-gradient(circle at 20% 80%, rgba(76, 29, 149, 0.15), transparent 50%);
      color: var(--text);
      font-family: 'Inter', sans-serif;
      min-height: 100vh;
      overflow-x: hidden;
      line-height: 1.6;
    }
    .container {
      max-width: 1200px;
      margin: 0 auto;
      padding: 40px 24px;
    }
    header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding-bottom: 30px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
      flex-wrap: wrap;
      gap: 16px;
    }
    .logo-group {
      display: flex;
      align-items: center;
      gap: 16px;
    }
    .serpent-icon {
      width: 44px;
      height: 44px;
      background: linear-gradient(135deg, #40e0d0, #a855f7);
      border-radius: 12px;
      display: flex;
      align-items: center;
      justify-content: center;
      box-shadow: 0 0 20px var(--cyan-glow);
    }
    .serpent-icon svg {
      width: 26px;
      height: 26px;
      fill: #fff;
    }
    .brand-title {
      font-family: 'Cinzel', serif;
      font-size: 1.5rem;
      font-weight: 800;
      letter-spacing: 0.08em;
      background: linear-gradient(120deg, #ffffff, #40e0d0 70%, #ffd700);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
    }
    .status-badge {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      background: rgba(64, 255, 128, 0.1);
      border: 1px solid rgba(64, 255, 128, 0.3);
      padding: 6px 14px;
      border-radius: 999px;
      font-size: 0.85rem;
      font-weight: 600;
      color: var(--green);
      font-family: 'JetBrains Mono', monospace;
    }
    .pulse-dot {
      width: 8px;
      height: 8px;
      background: var(--green);
      border-radius: 50%;
      box-shadow: 0 0 8px var(--green);
      animation: pulse 2s infinite;
    }
    @keyframes pulse {
      0%, 100% { opacity: 1; transform: scale(1); }
      50% { opacity: 0.4; transform: scale(0.85); }
    }
    .hero {
      text-align: center;
      padding: 60px 0 40px;
    }
    .hero h1 {
      font-family: 'Cinzel', serif;
      font-size: clamp(2rem, 5vw, 3.4rem);
      font-weight: 900;
      letter-spacing: 0.05em;
      margin-bottom: 12px;
      text-shadow: 0 0 35px var(--cyan-glow);
    }
    .hero p {
      font-size: clamp(1rem, 2vw, 1.25rem);
      color: var(--text-muted);
      max-width: 750px;
      margin: 0 auto 28px;
    }
    .hero-tags {
      display: flex;
      justify-content: center;
      gap: 12px;
      flex-wrap: wrap;
    }
    .tag {
      background: rgba(255, 255, 255, 0.04);
      border: 1px solid rgba(255, 255, 255, 0.1);
      padding: 6px 14px;
      border-radius: 8px;
      font-size: 0.85rem;
      color: var(--cyan);
      font-family: 'JetBrains Mono', monospace;
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(270px, 1fr));
      gap: 20px;
      margin: 40px 0;
    }
    .card {
      background: var(--card-bg);
      backdrop-filter: blur(12px);
      border: 1px solid var(--card-border);
      border-radius: 16px;
      padding: 24px;
      transition: transform 0.2s ease, border-color 0.2s ease, box-shadow 0.2s ease;
    }
    .card:hover {
      transform: translateY(-3px);
      border-color: rgba(64, 224, 208, 0.45);
      box-shadow: 0 8px 30px rgba(0, 0, 0, 0.4);
    }
    .card-icon {
      font-size: 1.8rem;
      margin-bottom: 12px;
    }
    .card h3 {
      font-size: 1.15rem;
      margin-bottom: 8px;
      color: #fff;
    }
    .card p {
      font-size: 0.9rem;
      color: var(--text-muted);
    }
    .card .metric {
      font-family: 'JetBrains Mono', monospace;
      font-size: 1.4rem;
      font-weight: 700;
      color: var(--cyan);
      margin: 10px 0 4px;
    }
    .section-title {
      font-family: 'Cinzel', serif;
      font-size: 1.6rem;
      margin: 50px 0 20px;
      display: flex;
      align-items: center;
      gap: 12px;
    }
    .section-title::after {
      content: '';
      flex: 1;
      height: 1px;
      background: rgba(255, 255, 255, 0.1);
    }
    .leaderboard-box {
      background: var(--card-bg);
      border: 1px solid var(--card-border);
      border-radius: 16px;
      overflow: hidden;
      margin-bottom: 40px;
    }
    .tabs {
      display: flex;
      background: rgba(0, 0, 0, 0.3);
      border-bottom: 1px solid var(--card-border);
    }
    .tab-btn {
      flex: 1;
      padding: 14px 20px;
      background: transparent;
      border: none;
      color: var(--text-muted);
      font-family: 'Cinzel', serif;
      font-size: 1rem;
      font-weight: 700;
      cursor: pointer;
      transition: all 0.2s;
    }
    .tab-btn.active {
      color: var(--cyan);
      background: rgba(64, 224, 208, 0.08);
      border-bottom: 2px solid var(--cyan);
    }
    .table-container {
      padding: 16px;
      overflow-x: auto;
    }
    table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.92rem;
    }
    th {
      text-align: left;
      padding: 12px 16px;
      color: var(--text-muted);
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
      font-family: 'JetBrains Mono', monospace;
      font-size: 0.8rem;
      text-transform: uppercase;
    }
    td {
      padding: 14px 16px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.04);
    }
    tr:last-child td { border-bottom: none; }
    .rank-badge {
      font-weight: 700;
      font-family: 'JetBrains Mono', monospace;
    }
    .rank-1 { color: #ffd700; text-shadow: 0 0 10px rgba(255, 215, 0, 0.4); }
    .rank-2 { color: #c0c0c0; }
    .rank-3 { color: #cd7f32; }
    .score-val {
      font-family: 'JetBrains Mono', monospace;
      font-weight: 700;
      color: var(--cyan);
    }
    .evo-badge {
      padding: 3px 10px;
      border-radius: 999px;
      font-size: 0.75rem;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }
    .evo-titan { background: rgba(239, 68, 68, 0.15); color: #f87171; border: 1px solid rgba(239, 68, 68, 0.3); }
    .evo-celestial { background: rgba(56, 189, 248, 0.15); color: #38bdf8; border: 1px solid rgba(56, 189, 248, 0.3); }
    .evo-abyssal { background: rgba(168, 85, 247, 0.15); color: #c084fc; border: 1px solid rgba(168, 85, 247, 0.3); }
    .evo-primal { background: rgba(156, 163, 175, 0.15); color: #9ca3af; border: 1px solid rgba(156, 163, 175, 0.3); }
    .api-console {
      background: rgba(0, 0, 0, 0.5);
      border: 1px solid var(--card-border);
      border-radius: 12px;
      padding: 20px;
      font-family: 'JetBrains Mono', monospace;
      font-size: 0.85rem;
      margin-top: 20px;
    }
    .btn-group {
      display: flex;
      gap: 10px;
      margin-bottom: 16px;
      flex-wrap: wrap;
    }
    .api-btn {
      background: rgba(64, 224, 208, 0.12);
      border: 1px solid rgba(64, 224, 208, 0.3);
      color: var(--cyan);
      padding: 8px 16px;
      border-radius: 8px;
      cursor: pointer;
      font-family: 'JetBrains Mono', monospace;
      font-size: 0.85rem;
      transition: all 0.2s;
    }
    .api-btn:hover {
      background: rgba(64, 224, 208, 0.25);
      border-color: var(--cyan);
      color: #fff;
    }
    pre {
      background: #040508;
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 8px;
      padding: 16px;
      color: #38bdf8;
      overflow-x: auto;
      max-height: 250px;
    }
    footer {
      text-align: center;
      padding: 40px 0 20px;
      border-top: 1px solid rgba(255, 255, 255, 0.08);
      color: var(--text-muted);
      font-size: 0.88rem;
    }
    footer a {
      color: var(--cyan);
      text-decoration: none;
    }
    footer a:hover {
      text-decoration: underline;
    }
  </style>
</head>
<body>
  <div class="container">
    <header>
      <div class="logo-group">
        <div class="serpent-icon">
          <svg viewBox="0 0 24 24">
            <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z"/>
          </svg>
        </div>
        <div>
          <div class="brand-title">AETHER SERPENT</div>
          <div style="font-size: 0.78rem; color: var(--text-muted); letter-spacing: 0.12em; text-transform: uppercase;">Eternal Coil // Cloudflare Edge</div>
        </div>
      </div>
      <div class="status-badge">
        <span class="pulse-dot"></span>
        <span>EDGE STATUS: ONLINE</span>
      </div>
    </header>

    <section class="hero">
      <h1>CELESTIAL EDGE NETWORK</h1>
      <p>High-performance, distributed backend powered by Cloudflare Workers and SQLite-backed Durable Objects with WebSocket Hibernation.</p>
      <div class="hero-tags">
        <span class="tag">⚡ 20Hz Tick Simulation</span>
        <span class="tag">🛡️ Anti-Cheat & Turnstile</span>
        <span class="tag">🤝 2-Player Co-op Spine</span>
        <span class="tag">💾 SQLite Persistent State</span>
      </div>
    </section>

    <div class="grid">
      <div class="card">
        <div class="card-icon">🎮</div>
        <h3>GameRoom DO</h3>
        <p>20Hz real-time simulation loop merging Player A (Head) and Player B (Tail) inputs with WebSocket Hibernation.</p>
        <div class="metric">20 Hz</div>
        <p style="font-size: 0.8rem; color: var(--green);">● Active & Authoritative</p>
      </div>

      <div class="card">
        <div class="card-icon">🏆</div>
        <h3>SocialHub DO</h3>
        <p>Embedded SQLite storage governing Global & Weekly rankings, Friends lists, and atomic cloud save slots.</p>
        <div class="metric">SQLite DO</div>
        <p style="font-size: 0.8rem; color: var(--green);">● Edge Persistent</p>
      </div>

      <div class="card">
        <div class="card-icon">🛡️</div>
        <h3>Security & Auth</h3>
        <p>HMAC-SHA256 JWT tokens, sliding-window rate limiting, and server-side Turnstile verification.</p>
        <div class="metric">JWT / 15m</div>
        <p style="font-size: 0.8rem; color: var(--green);">● Anti-Cheat Clamping</p>
      </div>

      <div class="card">
        <div class="card-icon">🌍</div>
        <h3>WorldZone DO</h3>
        <p>Spatial sharding foundation for seamless open-world exploration and celestial ecosystems.</p>
        <div class="metric">Ready</div>
        <p style="font-size: 0.8rem; color: var(--cyan);">● Shard Scalable</p>
      </div>
    </div>

    <h2 class="section-title">🏆 Live Celestial Leaderboards</h2>

    <div class="leaderboard-box">
      <div class="tabs">
        <button class="tab-btn active" id="tabGlobal" onclick="loadTab('global')">Global All-Time Ranks</button>
        <button class="tab-btn" id="tabWeekly" onclick="loadTab('weekly')">Current Weekly Ranks</button>
      </div>
      <div class="table-container">
        <table>
          <thead>
            <tr>
              <th>Rank</th>
              <th>Drifter Name</th>
              <th>High Score</th>
              <th>Length</th>
              <th>Evolution</th>
              <th>Recorded At</th>
            </tr>
          </thead>
          <tbody id="leaderboardBody">
            <tr>
              <td colspan="6" style="text-align: center; color: var(--text-muted); padding: 30px;">
                Loading live rankings from Cloudflare SocialHubDO...
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <h2 class="section-title">📡 Live Edge API Console</h2>
    <div class="api-console">
      <p style="margin-bottom: 12px; color: var(--text-muted);">Quick-test backend endpoints directly from this edge server:</p>
      <div class="btn-group">
        <button class="api-btn" onclick="testEndpoint('/api/health')">GET /api/health</button>
        <button class="api-btn" onclick="testEndpoint('/api/system/version-check')">GET /api/system/version-check</button>
        <button class="api-btn" onclick="testEndpoint('/api/leaderboard/global?limit=5')">GET /api/leaderboard/global</button>
        <button class="api-btn" onclick="testEndpoint('/api/leaderboard/weekly?limit=5')">GET /api/leaderboard/weekly</button>
      </div>
      <pre id="apiOutput">// Click an endpoint above to execute a real-time request...</pre>
    </div>

    <footer>
      <p>AetherSerpent: Eternal Coil &copy; 2026. Powered by Unity 6 & Cloudflare Workers.</p>
      <p style="margin-top: 6px;">GitHub Repository: <a href="https://github.com/uttombarman55-crypto/AetherSerpent" target="_blank" rel="noopener">uttombarman55-crypto/AetherSerpent</a></p>
    </footer>
  </div>

  <script>
    let currentTab = 'global';

    async function loadTab(tab) {
      currentTab = tab;
      document.getElementById('tabGlobal').classList.toggle('active', tab === 'global');
      document.getElementById('tabWeekly').classList.toggle('active', tab === 'weekly');
      
      const tbody = document.getElementById('leaderboardBody');
      tbody.innerHTML = '<tr><td colspan="6" style="text-align: center; color: var(--text-muted); padding: 30px;">Querying live rankings...</td></tr>';

      try {
        const res = await fetch('/api/leaderboard/' + tab + '?limit=10');
        const data = await res.json();
        
        if (!data.entries || data.entries.length === 0) {
          tbody.innerHTML = '<tr><td colspan="6" style="text-align: center; color: var(--text-muted); padding: 30px;">No rankings recorded yet for this category. Play in Unity to claim the crown!</td></tr>';
          return;
        }

        tbody.innerHTML = data.entries.map((e, idx) => {
          const rank = idx + 1;
          const rankClass = rank === 1 ? 'rank-1' : (rank === 2 ? 'rank-2' : (rank === 3 ? 'rank-3' : ''));
          const evoClass = e.evolutionPath === 1 ? 'evo-titan' : (e.evolutionPath === 2 ? 'evo-celestial' : (e.evolutionPath === 3 ? 'evo-abyssal' : 'evo-primal'));
          const evoName = e.evolutionPath === 1 ? 'Titan' : (e.evolutionPath === 2 ? 'Celestial' : (e.evolutionPath === 3 ? 'Abyssal' : 'Primal'));
          const dateStr = new Date(e.submittedAt).toLocaleString();

          return \`<tr>
            <td><span class="rank-badge \${rankClass}">#\${rank}</span></td>
            <td><strong>\${escapeHtml(e.username)}</strong></td>
            <td><span class="score-val">\${Number(e.score).toLocaleString()}</span></td>
            <td>\${e.length} seg</td>
            <td><span class="evo-badge \${evoClass}">\${evoName}</span></td>
            <td style="color: var(--text-muted); font-size: 0.85rem;">\${dateStr}</td>
          </tr>\`;
        }).join('');
      } catch (err) {
        tbody.innerHTML = '<tr><td colspan="6" style="text-align: center; color: #f87171; padding: 30px;">Failed loading rankings: ' + err.message + '</td></tr>';
      }
    }

    async function testEndpoint(endpoint) {
      const output = document.getElementById('apiOutput');
      output.textContent = 'Calling ' + endpoint + '...';
      try {
        const res = await fetch(endpoint);
        const data = await res.json();
        output.textContent = JSON.stringify(data, null, 2);
      } catch (err) {
        output.textContent = 'Error: ' + err.message;
      }
    }

    function escapeHtml(str) {
      return (str || '').replace(/[&<>"']/g, function(m) {
        return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' }[m];
      });
    }

    // Initial load
    loadTab('global');
  </script>
</body>
</html>`;
}

