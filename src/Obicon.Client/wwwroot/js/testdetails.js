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
    if (details.tls) {
        return renderTlsDetails(details.tls);
    }
    return '';
}

function renderTracerouteDetails(traceroute) {
    const rows = (traceroute.hops || []).map(hop => {
        const rtts = (hop.probes || [])
            .map(probe => probe.roundtripMs != null ? `${probe.roundtripMs}` : '*')
            .join(' / ');
        const address = hop.address
            ? (hop.hostname ? `${tdEscapeHtml(hop.hostname)} (${tdEscapeHtml(hop.address)})` : tdEscapeHtml(hop.address))
            : '*';
        return `
        <tr>
            <td>${tdEscapeHtml(hop.hop)}</td>
            <td class="text-break">${address}</td>
            <td>${tdEscapeHtml(hop.status)}</td>
            <td>${rtts ? tdEscapeHtml(rtts + ' ms') : '-'}</td>
        </tr>`;
    }).join('');

    return `
        <div class="mt-1">
            ${tdPairs([
                tdPair('resolved', traceroute.resolvedAddress),
                tdPair('target reached', traceroute.targetReached ? 'yes' : 'no'),
                tdPair('hops', traceroute.hopCount),
                tdPair('error', traceroute.error)
            ])}
            <table class="table table-sm table-striped small mb-0 mt-1">
                <thead><tr><th>Hop</th><th>Host</th><th>Status</th><th>RTT per probe</th></tr></thead>
                <tbody>${rows}</tbody>
            </table>
        </div>`;
}

function renderPingDetails(ping) {
    const stats = tdPairs([
        tdPair('resolved', ping.resolvedAddress),
        tdPair('dns', tdMs(ping.dnsMs)),
        tdPair('reply from', ping.replyAddress),
        tdPair('status', ping.replyStatus),
        tdPair('sent', ping.sent),
        tdPair('received', ping.received),
        tdPair('loss', ping.lossPercent != null ? ping.lossPercent + '%' : null),
        tdPair('min/avg/max', ping.minRoundtripMs != null ? `${ping.minRoundtripMs}/${ping.avgRoundtripMs}/${ping.maxRoundtripMs} ms` : null),
        tdPair('ttl', ping.ttl),
        tdPair('wall clock', tdMs(ping.wallclockMs)),
        tdPair('error', ping.error)
    ]);

    const rows = (ping.replies || []).map((reply, index) => `
        <tr>
            <td>${index + 1}</td>
            <td class="text-break">${tdEscapeHtml(reply.replyAddress || '*')}</td>
            <td>${tdEscapeHtml(reply.replyStatus)}</td>
            <td>${reply.roundtripMs != null ? tdEscapeHtml(tdMs(reply.roundtripMs)) : '-'}</td>
            <td>${reply.ttl != null ? tdEscapeHtml(reply.ttl) : '-'}</td>
        </tr>`).join('');

    return `
        <div class="mt-1">
            ${stats}
            ${rows ? `<table class="table table-sm table-striped small mb-0 mt-1">
                <thead><tr><th>#</th><th>Reply from</th><th>Status</th><th>RTT</th><th>TTL</th></tr></thead>
                <tbody>${rows}</tbody>
            </table>` : ''}
        </div>`;
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
                tdPair('method', http.method),
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
    // Records with their type and TTL when the resolver reported one
    const recordChips = (dns.records || []).map(record =>
        `<code>${tdEscapeHtml(record.recordType)} ${tdEscapeHtml(record.value)}${record.ttlSeconds != null && record.ttlSeconds >= 0 ? ` <span class="text-body">(${record.ttlSeconds}s)</span>` : ''}</code>`);
    const recordPairs = recordChips.join(' ');

    return `
        <div class="mt-1">
            ${tdPairs([
                tdPair('via', dns.via),
                tdPair('query type', dns.queryType),
                tdPair('status', dns.responseStatus),
                tdPair('nameservers', (dns.nameserversQueried || []).join(', ')),
                tdPair('answered by', dns.answeringNameserver),
                tdPair('rtt', tdMs(dns.nameserverRttMs)),
                tdPair('expected', dns.expectedMatched == null ? dns.expectedAddress : `${dns.expectedAddress} (${dns.expectedMatched ? 'match' : 'no match'})`),
                tdPair('error', dns.error)
            ])}
            ${recordPairs ? `<div class="small text-muted">${recordPairs}</div>` : ''}
        </div>`;
}

function renderTlsDetails(tls) {
    const certificate = tls.certificate;
    const certificatePairs = certificate
        ? tdPairs([
            tdPair('cert subject', certificate.subject),
            tdPair('issuer', certificate.issuer),
            tdPair('valid from', certificate.notBefore ? new Date(certificate.notBefore).toISOString().substring(0, 10) : null),
            tdPair('expires', certificate.notAfter ? new Date(certificate.notAfter).toISOString().substring(0, 10) : null),
            tdPair('days left', certificate.daysRemaining),
            (certificate.subjectAlternativeNames || []).length > 0
                ? `<span class="text-nowrap"><span class="text-body">SANs</span>: ${(certificate.subjectAlternativeNames).map(n => `<code>${tdEscapeHtml(n)}</code>`).join(' ')}</span>`
                : ''
        ])
        : '';

    return `
        <div class="mt-1">
            ${tdPairs([
                tdPair('host', tls.host != null ? `${tls.host}:${tls.port}` : null),
                tdPair('resolved', tls.resolvedAddress),
                tdPair('family', tls.family),
                tdPair('dns', tdMs(tls.dnsMs)),
                tdPair('connect', tdMs(tls.connectMs)),
                tdPair('handshake', tdMs(tls.handshakeMs)),
                tdPair('protocol', tls.protocol),
                tdPair('cipher', tls.cipher),
                tdPair('error', tls.error)
            ])}
            ${certificatePairs}
        </div>`;
}
