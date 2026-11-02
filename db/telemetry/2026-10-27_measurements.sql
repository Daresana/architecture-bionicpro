SET client_encoding = 'UTF8';

CREATE TABLE measurements
(
    mea_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    mea_prosthesis_id integer NOT NULL,
    mea_created_at timestamptz NOT NULL,
    mea_movement_count integer NOT NULL CHECK (mea_movement_count >= 0),
    mea_signal double precision NOT NULL,
    mea_battery double precision NOT NULL CHECK (mea_battery BETWEEN 0 AND 100)
);

CREATE INDEX measurements_created_at_idx ON measurements (mea_created_at);

COMMENT ON TABLE measurements IS 'Учебная телеметрия протезов';
COMMENT ON COLUMN measurements.mea_id IS 'Идентификатор измерения';
COMMENT ON COLUMN measurements.mea_prosthesis_id IS 'Протез из отдельной CRM';
COMMENT ON COLUMN measurements.mea_created_at IS 'Время измерения';
COMMENT ON COLUMN measurements.mea_movement_count IS 'Число движений за интервал';
COMMENT ON COLUMN measurements.mea_signal IS 'Уровень миосигнала в условных единицах';
COMMENT ON COLUMN measurements.mea_battery IS 'Заряд батареи в процентах';
