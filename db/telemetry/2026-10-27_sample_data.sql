INSERT INTO measurements
    (mea_prosthesis_id, mea_created_at, mea_movement_count, mea_signal, mea_battery)
SELECT
    prosthesis_id,
    sample_time,
    (extract(hour FROM sample_time)::integer + prosthesis_id) % 12,
    0.4 + (prosthesis_id % 10) * 0.01,
    100 - extract(hour FROM sample_time) * 2
FROM generate_series(
    CURRENT_DATE - INTERVAL '7 days',
    CURRENT_DATE - INTERVAL '15 minutes',
    INTERVAL '15 minutes'
) AS sample_time
CROSS JOIN unnest(ARRAY[101, 102, 201, 301]) AS prosthesis_id;
