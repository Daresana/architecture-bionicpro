CREATE DATABASE IF NOT EXISTS reports;

CREATE TABLE IF NOT EXISTS reports.usage_reports
(
    user_id String,
    report_date Date,
    prosthesis_id UInt32,
    client_name String,
    movement_count UInt64,
    average_signal Float64,
    average_battery Float64,
    batch_id String,
    version UInt64
)
ENGINE = ReplacingMergeTree(version)
PARTITION BY toYYYYMM(report_date)
ORDER BY (user_id, report_date, prosthesis_id, batch_id);

CREATE TABLE IF NOT EXISTS reports.report_periods
(
    report_date Date,
    batch_id String,
    version UInt64
)
ENGINE = ReplacingMergeTree(version)
ORDER BY report_date;

CREATE USER IF NOT EXISTS reports_api IDENTIFIED WITH sha256_password BY 'reports_read_local';
GRANT SELECT ON reports.usage_reports TO reports_api;
GRANT SELECT ON reports.report_periods TO reports_api;
