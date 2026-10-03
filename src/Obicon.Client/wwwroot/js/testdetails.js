// Structured test result details rendering, shared by the queue page and dry runs.
// Renders the `details` object reported by nodes (0.4.0+) per test type; returns an
// empty string when absent so callers can fall back to the legacy metrics line.

function tdEscapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text == null ? '' : String(text);
    return div.innerHTML;
}

// Formats a nullable millisecond value compactly: 12.3 ms
function tdMs(value) {
    if (value == null) {
        return null;
    }
    return `${Number(value)} ms`;
}

// One muted "label=value" segment, skipped when the value is missing
function tdPair(label, value) {
    if (value == null || value === '') {
        return '';
    }
    return `<span class="text-nowrap"><span class="text-body">${tdEscapeHtml(label)}</span>=<span>${tdEscapeHtml(value)}</span></span>`;
}

// A muted segments line: label=value · label=value
function tdPairs(pairs) {
    const rendered = pairs.filter(Boolean).join(' · ');
    return rendered ? `<div class="small text-muted">${rendered}</div>` : '';
}

function renderTestDetails(details) {
    if (!details) {
        return '';
    }
    if (details.traceroute) {
        return renderTracerouteDetails(details.traceroute);
    }
    if (details.ping) {
        return renderPingDetails(details.ping);
    }
    if (details.tcp) {
        return renderTcpDetails(details.tcp);
    }
    if (details.http) {
        return renderHttpDetails(details.http);
    }
    if (details.dns) {
        return renderDnsDetails(details.dns);
    }
    return '';
}

function renderTracerouteDetails(traceroute) {
    const rows = (traceroute.hops || []).map(hop => `
        <tr>
            <td>${tdEscapeHtml(hop.hop)}</td>
            <td class="text-break">${hop.address ? tdEscapeHtml(hop.address) : '*'}</td>
            <td>${tdEscapeHtml(hop.status)}</td>
            <td>${hop.roundtripMs != null ? tdEscapeHtml(tdMs(hop.roundtripMs)) : '-'}</td>
        </tr>`).join('');

    return `
        <div class="mt-1">
            ${tdPairs([
                tdPair('resolved', traceroute.resolvedAddress),
                tdPair('target reached', traceroute.targetReached ? 'yes' : 'no'),
                tdPair('hops', traceroute.hopCount),
                tdPair('error', traceroute.error)
            ])}
            <table class="table table-sm table-striped small mb-0 mt-1">
                <thead><tr><th>Hop</th><th>Address</th><th>Status</th><th>RTT</th></tr></thead>
                <tbody>${rows}</tbody>
            </table>
        </div>`;
}

function renderPingDetails(ping) {
    return tdPairs([
        tdPair('resolved', ping.resolvedAddress),
        tdPair('dns', tdMs(ping.dnsMs)),
        tdPair('reply from', ping.replyAddress),
        tdPair('status', ping.replyStatus),
        tdPair('roundtrip', tdMs(ping.roundtripMs)),
        tdPair('ttl', ping.ttl),
        tdPair('wall clock', tdMs(ping.wallclockMs)),
        tdPair('error', ping.error)
    ]);
}

function renderTcpDetails(tcp) {
    return tdPairs([
        tdPair('resolved', tcp.resolvedAddress),
        tdPair('family', tcp.family),
        tdPair('dns', tdMs(tcp.dnsMs)),
        tdPair('connect', tdMs(tcp.connectMs)),
        tdPair('error', tcp.error)
    ]);
}

function renderHttpDetails(http) {
    const certificate = http.certificate;
    const certPairs = certificate
        ? tdPairs([
            tdPair('cert subject', certificate.subject),
            tdPair('issuer', certificate.issuer),
            tdPair('expires', certificate.notAfter ? new Date(certificate.notAfter).toISOString().substring(0, 10) : null),
            tdPair('days left', certificate.daysRemaining)
        ])
        : '';

    return `
        <div class="mt-1">
            ${tdPairs([
                tdPair('status', http.statusCode != null ? `${http.statusCode} ${http.reasonPhrase || ''}`.trim() : null),
                tdPair('resolved', http.resolvedAddress),
                tdPair('dns', tdMs(http.dnsMs)),
                tdPair('connect', tdMs(http.connectMs)),
                tdPair('tls', tdMs(http.tlsMs)),
                tdPair('ttfb', tdMs(http.ttfbMs)),
                tdPair('transfer', tdMs(http.transferMs)),
                tdPair('bytes', http.bytesRead),
                http.bytesTruncated ? tdPair('body', 'truncated at 2 MB') : '',
                tdPair('protocol', http.tlsProtocol),
                tdPair('cipher', http.tlsCipher),
                tdPair('proxy', http.proxyUrl),
                tdPair('body pattern', http.bodyMatched == null ? null : (http.bodyMatched ? 'matched' : 'no match')),
                tdPair('error', http.error)
            ])}
            ${certPairs}
        </div>`;
}

function renderDnsDetails(dns) {
    const recordList = (label, records) => records && records.length > 0
        ? `<span class="text-nowrap"><span class="text-body">${label}</span>: ${records.map(r => `<code>${tdEscapeHtml(r)}</code>`).join(' ')}</span>`
        : '';
    const recordPairs = [recordList('A', dns.aRecords), recordList('AAAA', dns.aaaaRecords)]
        .filter(Boolean)
        .join(' · ');

    return `
        <div class="mt-1">
            ${tdPairs([
                tdPair('via', dns.via),
                tdPair('nameservers', (dns.nameserversQueried || []).join(', ')),
                tdPair('answered by', dns.answeringNameserver),
                tdPair('rtt', tdMs(dns.nameserverRttMs)),
                tdPair('expected', dns.expectedMatched == null ? dns.expectedAddress : `${dns.expectedAddress} (${dns.expectedMatched ? 'match' : 'no match'})`),
                tdPair('error', dns.error)
            ])}
            ${recordPairs ? `<div class="small text-muted">${recordPairs}</div>` : ''}
        </div>`;
}
