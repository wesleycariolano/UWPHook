-- Metadata-only distribution policy. UWPHook is a .NET Xbox launcher and does
-- not initialize Steamworks, so no steam_appid.txt maintenance is applicable.
-- Return a confirmed result (without `skipped`, which the frontend rejects).
return function(cjson)
    local M = {}
    function M.disabled()
        return cjson.encode({ ok = false, error = 'disabled_in_uwphook_distribution' })
    end

    local function launcher_result(request_json)
        local parsed, request = pcall(cjson.decode, tostring(request_json or ''))
        if not parsed or type(request) ~= 'table' or type(request.exe_path) ~= 'string' then
            return cjson.encode({ ok = false, error = 'invalid_request' })
        end
        local path = request.exe_path:match('^%s*(.-)%s*$')
        if path:sub(1, 1) == '"' and path:sub(-1) == '"' then path = path:sub(2, -2) end
        path = path:gsub('/', '\\'):lower()
        local absolute = path:match('^%a:\\') or path:match('^\\\\[^\\]+\\[^\\]+\\')
        local name = path:match('([^\\]+)$')
        if not absolute or (name ~= 'uwphook.exe' and name ~= 'sgs-app.exe') then
            return M.disabled()
        end
        return cjson.encode({ ok = true, modified = false, restored = false,
            reason = 'uwphook_launcher_no_steamworks' })
    end

    function M.neutralize(request_json) return launcher_result(request_json) end
    function M.restore(request_json) return launcher_result(request_json) end
    return M
end
