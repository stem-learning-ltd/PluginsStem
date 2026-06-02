// Namespace
window.AmbGrid = (function () {
    const ALIAS_AA = "aa"; // amb_activityambassador
    const ALIAS_A = "a";  // amb_ambassador
    const ALIAS_U = "u";  // amb_user
    const ALIAS_H = "h";  // amb_hub

    let rows = []; // cache for export

    function getXrm() {
        // Use parent Xrm to reach the hosting form (UCI friendly)
        return window.parent && window.parent.Xrm ? window.parent.Xrm : Xrm;
    }

    function getCurrentRecordId() {
        // GUID without braces
        try {
            const id = getXrm().Page.data.entity.getId();           // works in UCI
            return id ? id.replace(/[{}]/g, "") : null;
        } catch (e) {
            // Fallback: try URL param
            const p = new URLSearchParams(window.location.search);
            return (p.get("id") || "").replace(/[{}]/g, "");
        }
    }

    function td(s) { return `<td>${s ?? ""}</td>`; }
    function th(s) { return `<th>${s}</th>`; }

    function aliased(entity, alias, attr) {
        // Web API encodes '.' as '_x002e_'
        return entity[`${alias}_x002e_${attr}`];
    }

    function buildFetchXml(activityId) {
        return `
      <fetch version="1.0" distinct="true" top="50">
        <entity name="amb_activity">
          <attribute name="amb_activityid" />
          <filter>
            <condition attribute="amb_activityid" operator="eq" value="${activityId}" />
          </filter>
          <link-entity name="amb_activityambassador" from="amb_activityid" to="amb_activityid" link-type="inner" alias="${ALIAS_AA}">
            <attribute name="amb_state" />
            <link-entity name="amb_ambassador" from="amb_ambassadorid" to="amb_ambassadorid" link-type="inner" alias="${ALIAS_A}">
              <attribute name="amb_name" />
              <link-entity name="amb_user" from="amb_userid" to="amb_userid" link-type="inner" alias="${ALIAS_U}">
                <attribute name="amb_emailaddress" />
              </link-entity>
              <link-entity name="amb_hub" from="amb_hubid" to="amb_hub" link-type="inner" alias="${ALIAS_H}">
                <attribute name="amb_name" />
              </link-entity>
            </link-entity>
          </link-entity>
        </entity>
      </fetch>`;
    }

    async function loadStateLabels() {
        // Map option value -> label for the global choice "Activity Ambassador State Choice"
        const url = getXrm().Utility.getGlobalContext().getClientUrl() +
            "/api/data/v9.2/GlobalOptionSetDefinitions(Name='Activity Ambassador State Choice')";
        const r = await fetch(url, { headers: { "Accept": "application/json" } });
        const j = await r.json();
        const map = {};
        (j.Options || []).forEach(o => { map[o.Value] = o.Label?.UserLocalizedLabel?.Label; });
        return map;
    }

    async function run() {
        const xrm = getXrm();
        const id = getCurrentRecordId();
        const status = document.getElementById("status");
        const grid = document.getElementById("grid");
        grid.innerHTML = "";
        rows = [];

        if (!id) {
            status.textContent = "No record id available.";
            return;
        }

        status.textContent = "Loading...";
        const fetchXml = buildFetchXml(id);

        const result = await xrm.WebApi.retrieveMultipleRecords(
            "amb_activity", "?fetchXml=" + encodeURIComponent(fetchXml)
        );

        const stateMap = await loadStateLabels();

        // Build rows (usually one row because you said 1:1)
        rows = result.entities.map(e => ({
            state: stateMap[aliased(e, ALIAS_AA, "amb_state")] || "",
            hubName: aliased(e, ALIAS_H, "amb_name") || "",
            ambassadorName: aliased(e, ALIAS_A, "amb_name") || "",
            email: aliased(e, ALIAS_U, "amb_emailaddress") || ""
        }));

        // Render table
        let html = `<table><thead><tr>
      ${th("State")}${th("Hub")}${th("Ambassador")}${th("Email")}
    </tr></thead><tbody>`;
        rows.forEach(r => {
            html += `<tr>${td(r.state)}${td(r.hubName)}${td(r.ambassadorName)}${td(r.email)}</tr>`;
        });
        html += `</tbody></table>`;

        grid.innerHTML = html;
        status.textContent = rows.length ? `Rows: ${rows.length}` : "No data.";
    }

    function exportCsv() {
        const header = ["State", "Hub", "Ambassador", "Email"];
        const lines = [header.join(",")].concat(
            rows.map(r => [r.state, r.hubName, r.ambassadorName, r.email]
                .map(v => `"${String(v ?? "").replace(/"/g, '""')}"`).join(","))
        );
        const blob = new Blob([lines.join("\r\n")], { type: "text/csv;charset=utf-8" });
        const a = document.createElement("a");
        a.href = URL.createObjectURL(blob);
        a.download = "ActivityAmbassadors.csv";
        a.click();
        URL.revokeObjectURL(a.href);
    }

    function wireUi() {
        document.getElementById("btnReload").addEventListener("click", run);
        document.getElementById("btnExport").addEventListener("click", exportCsv);
    }

    return {
        init: function () { wireUi(); run().catch(e => console.error(e)); }
    };
})();
